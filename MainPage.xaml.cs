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


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SecurityTesting
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string ServerUrlHome = "https://ua-gas-gisportal.southernco.com/portal/home/";
        private const string AppClientId = "oHvyHoTBFYyzwTXV";
        private const string OAuthRedirectUrl = "my-ags-app://auth";

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
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(PromptCredentialAsync);

            //Create Handler to monitor response end flags 
            ArcGISHttpClientHandler.HttpResponseEnd += ArcGISHttpClientHandler_HttpResponseEnd;

            //Force a login to the Portal
            ArcGISPortal arcgisPortal = await ArcGISPortal.CreateAsync(new Uri(ServerUrlHome), true);

            // Create new Map with basemap
            // Map myMap = new Map(BasemapStyle.ArcGISImageryStandard);

            // Assign the map to the MapView
            //MyMapView.Map = myMap;
        }

        // ChallengeHandler function that will be called whenever access to a secured resource is attempted
        public static async Task<Credential> PromptCredentialAsync(CredentialRequestInfo info)
        {
            Credential credential = null;

            try
            {
                // IOAuthAuthorizeHandler will challenge the user for OAuth credentials
                credential = await AuthenticationManager.Current.GenerateCredentialAsync(info.ServiceUri);
            }
            catch (OperationCanceledException)
            {
                // OAuth login was canceled, no need to display error to user.
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return credential;
        }

        private void ArcGISHttpClientHandler_HttpRequestBegin(object s, System.Net.Http.HttpRequestMessage r)
        {
            if (r.RequestUri.Host == "ua-gas-gisportal.southernco.com")
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
            }
        }

        private void ArcGISHttpClientHandler_HttpResponseEnd(object sender, HttpResponseEndEventArgs e)
        {
            if (e.Response.Headers.Server.ToString().Contains("BigIP"))
            {
                e.Response = new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
            }
        }

        private async void webView1_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs e)
        {
            if (e.Uri.AbsoluteUri.ToString() == ServerUrlHome)
            {
                ArcGISHttpClientHandler.HttpResponseEnd -= ArcGISHttpClientHandler_HttpResponseEnd;
                ArcGISHttpClientHandler.HttpRequestBegin += ArcGISHttpClientHandler_HttpRequestBegin;
            }
            else
                Console.WriteLine(e.Uri.AbsoluteUri.ToString());
        }
    }
}
