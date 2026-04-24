using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace HR.Web.Services
{
    /// <summary>
    /// Imports only the basic member fields that LinkedIn exposes through its free profile APIs.
    /// This data is used strictly to prefill candidate-owned profile fields and not for screening logic.
    /// </summary>
    public class LinkedInProfileImportService
    {
        private const string AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
        private const string TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
        private const string IdentityEndpoint = "https://api.linkedin.com/rest/identityMe";
        private const string BasicProfileScope = "r_profile_basicinfo";
        private const string DefaultApiVersion = "202510";

        private readonly ISettingsService _settingsService;

        public LinkedInProfileImportService()
            : this(new SettingsService())
        {
        }

        public LinkedInProfileImportService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(GetClientId()) &&
                   !string.IsNullOrWhiteSpace(GetClientSecret());
        }

        public string GetUnavailabilityReason()
        {
            if (!string.IsNullOrWhiteSpace(GetClientId()) &&
                string.IsNullOrWhiteSpace(GetClientSecret()))
            {
                return "LinkedIn import is missing the client secret.";
            }

            if (string.IsNullOrWhiteSpace(GetClientId()) &&
                !string.IsNullOrWhiteSpace(GetClientSecret()))
            {
                return "LinkedIn import is missing the client ID.";
            }

            return "LinkedIn import is not configured for this portal yet.";
        }

        public string BuildAuthorizationUrl(string redirectUri, string state)
        {
            if (!IsConfigured())
            {
                throw new InvalidOperationException("LinkedIn import is not configured.");
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new ArgumentException("A LinkedIn redirect URI is required.", "redirectUri");
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("A LinkedIn OAuth state value is required.", "state");
            }

            return string.Format(
                "{0}?response_type=code&client_id={1}&redirect_uri={2}&scope={3}&state={4}",
                AuthorizationEndpoint,
                HttpUtility.UrlEncode(GetClientId()),
                HttpUtility.UrlEncode(redirectUri),
                HttpUtility.UrlEncode(BasicProfileScope),
                HttpUtility.UrlEncode(state));
        }

        public LinkedInBasicProfileResult ImportProfile(string authorizationCode, string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(authorizationCode))
            {
                throw new ArgumentException("LinkedIn did not return an authorization code.", "authorizationCode");
            }

            var accessToken = ExchangeCodeForAccessToken(authorizationCode, redirectUri);
            return GetBasicProfile(accessToken);
        }

        private string ExchangeCodeForAccessToken(string authorizationCode, string redirectUri)
        {
            EnsureModernTls();

            var payload = string.Format(
                "grant_type=authorization_code&code={0}&redirect_uri={1}&client_id={2}&client_secret={3}",
                HttpUtility.UrlEncode(authorizationCode),
                HttpUtility.UrlEncode(redirectUri),
                HttpUtility.UrlEncode(GetClientId()),
                HttpUtility.UrlEncode(GetClientSecret()));

            var request = (HttpWebRequest)WebRequest.Create(TokenEndpoint);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            var requestBytes = Encoding.UTF8.GetBytes(payload);
            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(requestBytes, 0, requestBytes.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var responseText = reader.ReadToEnd();
                    var responseJson = JObject.Parse(responseText);
                    var accessToken = responseJson.Value<string>("access_token");
                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        throw new InvalidOperationException("LinkedIn did not return an access token.");
                    }

                    return accessToken;
                }
            }
            catch (WebException ex)
            {
                throw new InvalidOperationException(BuildLinkedInErrorMessage("token", ex), ex);
            }
        }

        private LinkedInBasicProfileResult GetBasicProfile(string accessToken)
        {
            EnsureModernTls();

            var request = (HttpWebRequest)WebRequest.Create(IdentityEndpoint);
            request.Method = "GET";
            request.Headers["Authorization"] = "Bearer " + accessToken;
            request.Headers["LinkedIn-Version"] = GetApiVersion();

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var responseText = reader.ReadToEnd();
                    var responseJson = JObject.Parse(responseText);
                    var basicInfo = responseJson["basicInfo"];

                    var result = new LinkedInBasicProfileResult
                    {
                        FirstName = ExtractLocalizedText(basicInfo != null ? basicInfo["firstName"] : null),
                        LastName = ExtractLocalizedText(basicInfo != null ? basicInfo["lastName"] : null),
                        Email = basicInfo != null ? basicInfo.Value<string>("primaryEmailAddress") : null,
                        ProfileUrl = basicInfo != null ? basicInfo.Value<string>("profileUrl") : null,
                        PhotoUrl = basicInfo != null
                            ? (string)basicInfo.SelectToken("profilePicture.croppedImage.downloadUrl")
                            : null
                    };

                    result.FullName = BuildFullName(result.FirstName, result.LastName);
                    return result;
                }
            }
            catch (WebException ex)
            {
                throw new InvalidOperationException(BuildLinkedInErrorMessage("profile", ex), ex);
            }
        }

        private string GetClientId()
        {
            return _settingsService.GetSetting("LinkedInClientId") ??
                   ConfigurationManager.AppSettings["LinkedInClientId"];
        }

        private string GetClientSecret()
        {
            return _settingsService.GetSetting("LinkedInClientSecret") ??
                   ConfigurationManager.AppSettings["LinkedInClientSecret"];
        }

        private string GetApiVersion()
        {
            return _settingsService.GetSetting("LinkedInApiVersion") ??
                   ConfigurationManager.AppSettings["LinkedInApiVersion"] ??
                   DefaultApiVersion;
        }

        private static string ExtractLocalizedText(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            var localized = token["localized"] as JObject;
            if (localized != null && localized.Properties().Any())
            {
                return localized.Properties().First().Value.ToString();
            }

            return token.Type == JTokenType.String ? token.ToString() : null;
        }

        private static string BuildFullName(string firstName, string lastName)
        {
            var parts = new[] { firstName, lastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();

            return parts.Length == 0 ? null : string.Join(" ", parts);
        }

        private static string BuildLinkedInErrorMessage(string stage, WebException ex)
        {
            var responseText = ReadErrorResponse(ex);
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return string.Format("LinkedIn {0} request failed.", stage);
            }

            try
            {
                var responseJson = JObject.Parse(responseText);
                var message = responseJson.Value<string>("error_description") ??
                              responseJson.Value<string>("message") ??
                              responseJson.Value<string>("error");

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return string.Format("LinkedIn {0} request failed: {1}", stage, message);
                }
            }
            catch
            {
                // Return a safe fallback below.
            }

            return string.Format("LinkedIn {0} request failed.", stage);
        }

        private static string ReadErrorResponse(WebException ex)
        {
            if (ex == null || ex.Response == null)
            {
                return null;
            }

            using (var responseStream = ex.Response.GetResponseStream())
            {
                if (responseStream == null)
                {
                    return null;
                }

                using (var reader = new StreamReader(responseStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void EnsureModernTls()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
    }

    [Serializable]
    public sealed class LinkedInBasicProfileResult
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ProfileUrl { get; set; }
        public string PhotoUrl { get; set; }

        public bool HasData
        {
            get
            {
                return !string.IsNullOrWhiteSpace(FullName) ||
                       !string.IsNullOrWhiteSpace(Email) ||
                       !string.IsNullOrWhiteSpace(ProfileUrl);
            }
        }
    }
}
