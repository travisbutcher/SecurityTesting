using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Http;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Web.Http.Filters;
using Windows.Web.Http;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using AuthenticationManager = Esri.ArcGISRuntime.Security.AuthenticationManager;
using HttpResponseMessage = System.Net.Http.HttpResponseMessage;
using Windows.UI.Core;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SecurityTesting
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string ServerUrlHome = "https://ua-gas-gisportal.southernco.com/portal/home/";
        private const string AuthorizePage = "rest/oauth2/authorize";
        private const string AppClientId = "oHvyHoTBFYyzwTXV";
        private const string OAuthRedirectUrl = "my-ags-app://auth";
        private const string WebMapId = "5aab989b2829419399a2a665fd710dd7";
        private bool cookiesSet = false;
        private bool portalLoggedIn = false;
        private bool authorizeLoad = false;

        // Task completion source to track a login attempt.
        private TaskCompletionSource<Credential> _loginTaskCompletionSource;

        public MainPage()
        {
            this.InitializeComponent();

            Initialize();
        }

        private async void Initialize()
        {
            //Force the Portal to Login
            // Set up the AuthenticationManager to use OAuth for secure ArcGIS Online requests.
            // Define the server information for ArcGIS Online


            //Define the server information for ArcGIS Online
            ServerInfo portalServerInfo = new ServerInfo(new Uri(ServerUrlHome))
            {
                TokenAuthenticationType = TokenAuthenticationType.OAuthAuthorizationCode,
                OAuthClientInfo = new OAuthClientInfo(AppClientId, new Uri(OAuthRedirectUrl))
            };

            // Register the ArcGIS Online server information with the AuthenticationManager
            AuthenticationManager.Current.RegisterServer(portalServerInfo);

            // Create a new ChallengeHandler that uses a method in this class to challenge for credentials
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(Challenge);

            //Create Handler to monitor response end flags 
            ArcGISHttpClientHandler.HttpRequestBegin += ArcGISHttpClientHandler_HttpRequestBegin;
            ArcGISHttpClientHandler.HttpResponseEnd += ArcGISHttpClientHandler_HttpResponseEnd;

            //Force a login to the Portal
            ArcGISPortal arcgisPortal = await ArcGISPortal.CreateAsync(new Uri(ServerUrlHome), true);

            //Create a Link to the Portal Item
            PortalItem portalItem = await PortalItem.CreateAsync(arcgisPortal, WebMapId);
            // Create new Map with basemap
            Map myMap = new Map(portalItem);

            // Assign the map to the MapView
            webView.Visibility = Visibility.Collapsed;
            MyMapView.Visibility = Visibility.Visible;
            MyMapView.Map = myMap;
        }

        private async Task<Credential> Challenge(CredentialRequestInfo info)
        {
            // Get user credentials (on the UI thread).
            if (this.Dispatcher == null)
            {
                // No current dispatcher, code is already running on the UI thread.
                return await GetUserCredentialsFromUI(info);
            }
            else
            {
                // Use the dispatcher to invoke the challenge UI.
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        await GetUserCredentialsFromUI(info);
                    }
                    catch
                    {
                        // Login was canceled or unsuccessful, dialog will close.
                    }
                });
            }

            // Use the task completion source to return the results.
            return await _loginTaskCompletionSource.Task;
        }

        private async Task<Credential> GetUserCredentialsFromUI(CredentialRequestInfo info)
        {
            // Show the login UI.
            try
            {
                // Return the task from the completion source.
                // When the login button on the UI is clicked, the info will be returned for creating the credential.
                //loginPanel.Visibility = Visibility.Visible;
                webView.Navigate(new Uri(ServerUrlHome));
                _loginTaskCompletionSource = new TaskCompletionSource<Credential>(webView.DataContext);
                return await _loginTaskCompletionSource.Task;
            }
            finally
            {
            }
        }


        private void ArcGISHttpClientHandler_HttpRequestBegin(object s, System.Net.Http.HttpRequestMessage r)
        {
            if (r.RequestUri.Host == "ua-gas-gisportal.southernco.com")
            {
                if (!cookiesSet)
                {
                    HttpBaseProtocolFilter myFilter = new HttpBaseProtocolFilter();
                    var cookieManager = myFilter.CookieManager;
                    HttpCookieCollection myCookieJar = cookieManager.GetCookies(new Uri("https://ua-gas-gisportal.southernco.com"));

                    HttpClientHandler httpClientHandler = ((ArcGISHttpRequestMessage)r).Handler as HttpClientHandler;

                    foreach (HttpCookie cook in myCookieJar)
                    {
                        Debug.WriteLine(cook.Name);
                        Debug.WriteLine(cook.Value);
                        Cookie cookie = new Cookie();
                        cookie.Name = cook.Name;
                        cookie.Value = cook.Value;
                        cookie.Domain = cook.Domain;

                        httpClientHandler.CookieContainer.Add(cookie);
                    }

                    //cookiesSet = true;
                }
            }
        }

        private void ArcGISHttpClientHandler_HttpResponseEnd(object sender, HttpResponseEndEventArgs e)
        {
            if (e.Response.Headers.Server.ToString().Contains("BigIP") && e.Response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                e.Response = new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
            }
        }
 
        private async void webView1_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs e)
        {
            Debug.WriteLine(e.Uri.AbsoluteUri);
            if (e.Uri.AbsoluteUri.ToString().Contains(AuthorizePage))
                authorizeLoad = true;

            if (e.Uri.AbsoluteUri.ToString() == ServerUrlHome && authorizeLoad)
            {
                // Create a new task completion source to return the user's login when complete.
                // Set the login UI data context (LoginInfo object) as the AsyncState so it can be retrieved later.
                // Create a token credential using the provided username and password.
                try
                {
                    // Create a challenge request for portal credentials (OAuth credential request for arcgis.com)
                    CredentialRequestInfo challengeRequest = new CredentialRequestInfo
                    {
                        // Use the OAuth authorization code workflow.
                        GenerateTokenOptions = new GenerateTokenOptions
                        {
                            TokenAuthenticationType = TokenAuthenticationType.OAuthAuthorizationCode
                        },

                        // Indicate the url (portal) to authenticate with (ArcGIS Online)
                        ServiceUri = new Uri(ServerUrlHome)
                    };
                    Credential userCredentials = await AuthenticationManager.Current.GetCredentialAsync(challengeRequest, true);
                    _loginTaskCompletionSource.TrySetResult(userCredentials);
                }
                catch (Exception ex)
                {
                    // This causes the login attempt to fail.
                    _loginTaskCompletionSource.TrySetException(ex);
                }
            }
            else
                Console.WriteLine(e.Uri.AbsoluteUri.ToString());
        }

        private void webView_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            //Try to load the page again
            webView.Navigate(new Uri(ServerUrlHome));
        }
    }
}
