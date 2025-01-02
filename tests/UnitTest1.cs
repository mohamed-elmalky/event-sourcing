using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using FluentAssertions;
using System.Net.Http.Json;
using PersonEvents;
using Shared;
using Models;

namespace tests;

public class UnitTest1 : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient _client;
    private const string _basePersonPath = "/participants/person";

    public UnitTest1(WebApplicationFactory<Program> fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task HealthCheck()
    {
        // Act
        var response = await _client.GetAsync("/participants");
        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task POST_Creates_Participant_With_Name_Only()
    {
        // Arrange
        var participant = new Person("some-id") { Name = "John Doe" };
        var json = JsonSerializer.Serialize(participant);
        var data = new StringContent(json, Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(_basePersonPath, data);
        // Assert
        response.EnsureSuccessStatusCode();

        // get the url of the created participant
        var location = response.Headers.Location;
        Console.WriteLine($"Location: {location}");
        // get the participant
        var response2 = await _client.GetAsync(location);
        response2.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task POST_Existing_SSN_Should_Fail()
    {
        // Arrange
        var participant = new Person("some-id") { Name = "John Doe", SSN = "123-45-6789" };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(_basePersonPath, data);
        // Assert
        response.EnsureSuccessStatusCode();
        var participantId = await response.Content.ReadFromJsonAsync<string>();

        // try to create another participant with the same SSN
        var response2 = await _client.PostAsync(_basePersonPath, data);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var responseContent = await response2.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseContent);
        var id = json["id"]?.ToString();
        id.Should().Be(participantId);
    }

    [Fact]
    public async Task POST_Existing_Name_And_Home_Phone_Should_Fail()
    {
        // Arrange
        var participant = new Person("some-id") { Name = "John Doe", HomePhone = "123-45-6789" };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(_basePersonPath, data);
        // Assert
        response.EnsureSuccessStatusCode();
        var participantId = await response.Content.ReadFromJsonAsync<string>();

        // try to create another participant with the same name and home phone
        var response2 = await _client.PostAsync(_basePersonPath, data);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var responseContent = await response2.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseContent);
        var id = json["id"]?.ToString();
        id.Should().Be(participantId);
    }

    [Fact]
    public async Task POST_Existing_Name_And_Cell_Phone_Should_Fail()
    {
        // Arrange
        var participant = new Person("some-id") { Name = "John Doe", MobilePhone = "123-45-6789" };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(_basePersonPath, data);
        // Assert
        response.EnsureSuccessStatusCode();
        var participantId = await response.Content.ReadFromJsonAsync<string>();

        // try to create another participant with the same name and cell phone
        var response2 = await _client.PostAsync(_basePersonPath, data);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var responseContent = await response2.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseContent);
        var id = json["id"]?.ToString();
        id.Should().Be(participantId);
    }

    [Fact]
    public async Task POST_Existing_Name_And_Address_Should_Fail()
    {
        // Arrange
        var participant = new Person("some-id") { Name = "John Doe", Address = new Address("123 Main St", "Ste 101", "Anytown", "NY", "USA", "75001") };
        var data = new StringContent(JsonSerializer.Serialize(participant), Encoding.UTF8, "application/json");
        // Act
        var response = await _client.PostAsync(_basePersonPath, data);
        // Assert
        response.EnsureSuccessStatusCode();
        var participantId = await response.Content.ReadFromJsonAsync<string>();

        // try to create another participant with the same name and address
        var response2 = await _client.PostAsync(_basePersonPath, data);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var responseContent = await response2.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseContent);
        var id = json["id"]?.ToString();
        id.Should().Be(participantId);
    }
}
