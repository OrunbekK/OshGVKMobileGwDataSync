using System.Net.Http.Headers;
using System.Text;

namespace MobileGwDataSync.Integration.OneC
{
    public class OneCAuthHandler : DelegatingHandler
    {
        private readonly string _username;
        private readonly string _password;

        public OneCAuthHandler(string username, string password)
        {
            _username = username;
            _password = password;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Добавляем Basic Authentication
            var authValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
