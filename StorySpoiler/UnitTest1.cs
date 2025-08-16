using System;
using System.Net;
using System.Text.Json;
using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;
using StorySpoiler.Models; // ApiResponseDTO, StoryDTO

namespace StorySpoiler
{
    [TestFixture]
    public class StorySpoilerTests
    {
        private RestClient client;
        private static string createdStoryId;
        private const string baseUrl = "https://d3s5nxhwblsjbi.cloudfront.net";

        [OneTimeSetUp]
        public void Setup()
        {
            string token = GetJwtToken("yourUser123", "YourStrongPass123!");
            var options = new RestClientOptions(baseUrl)
            {
                Authenticator = new JwtAuthenticator(token)
            };
            client = new RestClient(options);
        }

        private string GetJwtToken(string userName, string password)
        {
            var loginClient = new RestClient(baseUrl);
            var request = new RestRequest("/api/User/Authentication", Method.Post);
            request.AddJsonBody(new { userName, password });
            var response = loginClient.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Login failed. {(int)response.StatusCode} {response.StatusCode}. Body: {response.Content}");
            var json = JsonSerializer.Deserialize<JsonElement>(response.Content ?? "{}");
            return json.GetProperty("accessToken").GetString() ?? string.Empty;
        }

        [Test, Order(1)]
        public void CreateStory_ShouldReturnCreated()
        {
            var story = new StoryDTO
            {
                Title = $"New Story {DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Description = "Test story description",
                Url = ""
            };

            var request = new RestRequest("/api/Story/Create", Method.Post).AddJsonBody(story);
            var response = client.Execute<ApiResponseDTO>(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

            var msg = ExtractMsg(response.Content);
            StringAssert.Contains("Successfully created", msg);

            if (response.Data != null && !string.IsNullOrWhiteSpace(response.Data.StoryId))
                createdStoryId = response.Data.StoryId!;
            else
            {
                var json = JsonSerializer.Deserialize<JsonElement>(response.Content ?? "{}");
                createdStoryId = json.TryGetProperty("storyId", out var id) ? id.GetString() ?? "" : "";
            }

            Assert.That(createdStoryId, Is.Not.Empty, "storyId not returned.");
        }

        [Test, Order(2)]
        public void EditStoryTitle_ShouldReturnOk()
        {
            Assume.That(!string.IsNullOrWhiteSpace(createdStoryId), "Missing createdStoryId.");

            var body = new StoryDTO
            {
                Title = "Updated Story Title",
                Description = "Updated description",
                Url = ""
            };

            var request = new RestRequest($"/api/Story/Edit/{createdStoryId}", Method.Put).AddJsonBody(body);
            var response = client.Execute<ApiResponseDTO>(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var msg = response.Data?.Msg ?? ExtractMsg(response.Content);
            StringAssert.Contains("Successfully edited", msg);
        }

        [Test, Order(3)]
        public void GetAllStories_ShouldReturnList()
        {
            var request = new RestRequest("/api/Story/All", Method.Get);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using var doc = JsonDocument.Parse(response.Content ?? "[]");
            Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(doc.RootElement.GetArrayLength(), Is.GreaterThan(0));
        }

        [Test, Order(4)]
        public void DeleteStory_ShouldReturnOk()
        {
            Assume.That(!string.IsNullOrWhiteSpace(createdStoryId), "Missing createdStoryId.");

            var request = new RestRequest($"/api/Story/Delete/{createdStoryId}", Method.Delete);
            var response = client.Execute<ApiResponseDTO>(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var msg = response.Data?.Msg ?? ExtractMsg(response.Content);
            StringAssert.Contains("Deleted successfully", msg);
        }

        [Test, Order(5)]
        public void CreateStory_WithoutRequiredFields_ShouldReturnBadRequest()
        {
            var bad = new StoryDTO { Title = "", Description = "", Url = "" };

            var request = new RestRequest("/api/Story/Create", Method.Post).AddJsonBody(bad);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test, Order(6)]
        public void EditNonExistingStory_ShouldReturnNotFound()
        {
            var fakeId = "000000000000000000000000";
            var body = new StoryDTO { Title = "Valid Title", Description = "Valid Description", Url = "" };

            var request = new RestRequest($"/api/Story/Edit/{fakeId}", Method.Put).AddJsonBody(body);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            var msg = ExtractMsg(response.Content);
            StringAssert.Contains("No spoilers", msg);
        }

        [Test, Order(7)]
        public void DeleteNonExistingStory_ShouldReturnBadRequest()
        {
            var fakeId = "000000000000000000000000";

            var request = new RestRequest($"/api/Story/Delete/{fakeId}", Method.Delete);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            var msg = ExtractMsg(response.Content);
            StringAssert.Contains("Unable to delete this story spoiler", msg);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            client?.Dispose();
        }

        private static string ExtractMsg(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("msg", out var m) &&
                    m.ValueKind == JsonValueKind.String)
                {
                    return m.GetString() ?? body;
                }
            }
            catch { }
            return body;
        }
    }
}
