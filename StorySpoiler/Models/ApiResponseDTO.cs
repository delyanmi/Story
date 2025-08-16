using System.Text.Json.Serialization;

namespace StorySpoiler.Models
{
    public class ApiResponseDTO
    {
        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("foodId")]
        public string? StoryId { get; set; }
    }
}
