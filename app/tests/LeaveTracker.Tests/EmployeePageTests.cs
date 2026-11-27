using System.Net;
using System.Text.RegularExpressions;

namespace LeaveTracker.Tests;

/// <summary>
/// LT-003: the employee page and its assets are served, and the markup carries what keyboard and
/// screen-reader users need. Interactive behaviour is covered by the manual checklist in the test report.
/// </summary>
public class EmployeePageTests : IClassFixture<EmployeePageTests.Host>
{
    public sealed class Host : IDisposable
    {
        public TestAppFactory Factory { get; } = new();

        public void Dispose() => Factory.Dispose();
    }

    private readonly HttpClient _client;

    public EmployeePageTests(Host host) => _client = host.Factory.CreateClient();

    private async Task<string> GetTextAsync(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private Task<string> PageAsync() => GetTextAsync("/");

    /// <summary>Returns the opening tag of the element with the given id.</summary>
    private static string TagWithId(string html, string id)
    {
        var match = Regex.Match(html, $"<[a-z]+[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Element with id '{id}' not found on the page.");
        return match.Value;
    }

    // AC1: the start address shows the employee page with the picker at the top.
    [Fact]
    public async Task Start_address_serves_the_employee_page()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<label for=\"who\">Who are you?</label>", html);
        Assert.Contains("Ask for leave", html);
        Assert.Contains("My requests", html);
        // The picker sits in the header, before the main content.
        Assert.True(html.IndexOf("id=\"who\"", StringComparison.Ordinal) < html.IndexOf("<main", StringComparison.Ordinal));
    }

    // AC1: serving the page does not get in the way of the API.
    [Fact]
    public async Task Api_routes_still_work_alongside_static_files()
    {
        var health = await _client.GetAsync("/api/health");
        var employees = await _client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("application/json", health.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, employees.StatusCode);
    }

    // AC2: the picker starts with a "pick who you are" option, and the form is disabled until someone is picked.
    [Fact]
    public async Task Page_has_picker_and_form_disabled_until_picked()
    {
        var html = await PageAsync();

        Assert.Matches("<select id=\"who\"[^>]*>\\s*<option value=\"\">— Pick who you are —</option>", html);
        Assert.Contains(" disabled", TagWithId(html, "leave-fields"));
        Assert.Contains("Pick who you are to ask for leave.", html);
    }

    // AC10: every input, select and textarea has a matching label.
    [Fact]
    public async Task Every_input_has_a_label()
    {
        var html = await PageAsync();

        var controlIds = Regex.Matches(html, "<(input|select|textarea)\\b[^>]*\\bid=\"([^\"]+)\"", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[2].Value)
            .ToList();
        Assert.Equal(new[] { "who", "first-day", "last-day", "type", "reason" }, controlIds);
        Assert.All(controlIds, id => Assert.Contains($"<label for=\"{id}\">", html));
    }

    // AC6/AC7/AC10: an error area and a status area for screen readers; every field is linked to its error slot.
    [Fact]
    public async Task Page_has_error_and_status_regions()
    {
        var html = await PageAsync();

        Assert.Contains("role=\"alert\"", TagWithId(html, "form-errors"));
        var status = TagWithId(html, "form-status");
        Assert.Contains("role=\"status\"", status);
        Assert.Contains("aria-live=\"polite\"", status);

        foreach (var field in new[] { "first-day", "last-day", "type", "reason" })
        {
            Assert.Contains($"{field}-error", TagWithId(html, field));
            TagWithId(html, $"{field}-error");
        }
    }

    // AC8: the "My requests" table has every column the requirement lists.
    [Fact]
    public async Task My_requests_table_has_all_columns()
    {
        var html = await PageAsync();

        Assert.Contains("<caption", html);
        var headers = Regex.Matches(html, "<th scope=\"col\"[^>]*>([^<]+)</th>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "First day", "Last day", "Type", "Days", "Status", "Manager's comment" }, headers);
    }

    // AC9: the manager link exists, points to the manager page, and is hidden until a manager is picked.
    [Fact]
    public async Task Manager_link_is_hidden_by_default()
    {
        var link = TagWithId(await PageAsync(), "manager-link");

        Assert.StartsWith("<a ", link);
        Assert.Contains("href=\"manager.html\"", link);
        Assert.Contains(" hidden", link);
    }

    // AC10: keyboard focus is always visible.
    [Fact]
    public async Task Stylesheet_has_visible_focus_style()
    {
        var css = await GetTextAsync("/css/site.css");

        Assert.Matches(":focus-visible\\s*\\{[^}]*outline:\\s*3px solid", css);
    }
}
