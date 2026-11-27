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

    // The page's scripts and stylesheet are served with the right types (ES modules need a JavaScript type).
    [Theory]
    [InlineData("/css/site.css", "text/css")]
    [InlineData("/js/api.js", "text/javascript")]
    [InlineData("/js/identity.js", "text/javascript")]
    [InlineData("/js/format.js", "text/javascript")]
    [InlineData("/js/employee.js", "text/javascript")]
    public async Task Static_assets_are_served_with_correct_types(string path, string mediaType)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    // AC11: every status the API can return is shown as a word, and the CSS never relies on colour alone.
    [Fact]
    public async Task Status_is_rendered_as_text()
    {
        var format = await GetTextAsync("/js/format.js");
        Assert.Contains("pending: 'Pending'", format);
        Assert.Contains("approved: 'Approved'", format);
        Assert.Contains("rejected: 'Rejected'", format);

        // Each status style differs in border style as well as colour.
        var css = await GetTextAsync("/css/site.css");
        Assert.Matches("\\.status-pending\\s*\\{[^}]*border-style:\\s*dashed", css);
        Assert.Matches("\\.status-approved\\s*\\{[^}]*border-style:\\s*solid", css);
        Assert.Matches("\\.status-rejected\\s*\\{[^}]*border-style:\\s*double", css);
    }

    // AC3/AC4: the choice is remembered in this browser only, and storage problems never break the page.
    [Fact]
    public async Task Identity_is_remembered_in_local_storage_safely()
    {
        var identity = await GetTextAsync("/js/identity.js");

        Assert.Contains("'leaveTracker.employeeId'", identity);
        Assert.Contains("localStorage", identity);
        // Storage is touched only in the two guarded helpers (read, and write/forget), each wrapped in try/catch.
        Assert.Equal(3, Regex.Matches(identity, "window\\.localStorage\\.").Count);
        Assert.Matches("function rememberedEmployeeId\\(\\) \\{\\s*try \\{", identity);
        Assert.Matches("function rememberEmployeeId\\(id\\) \\{\\s*try \\{", identity);
        // An unknown remembered id is forgotten (AC4).
        Assert.Contains("rememberEmployeeId(null)", identity);
    }

    // Safety: nothing is loaded from the internet (project.md: no CDN), and user text is never inserted as HTML.
    [Fact]
    public async Task Frontend_uses_no_external_urls_or_innerHTML()
    {
        var files = new Dictionary<string, string>();
        foreach (var path in new[] { "/", "/css/site.css", "/js/api.js", "/js/identity.js", "/js/format.js", "/js/employee.js" })
        {
            files[path] = await GetTextAsync(path);
        }

        Assert.All(files, f => Assert.DoesNotMatch("https?://", f.Value));
        Assert.All(files.Where(f => f.Key.EndsWith(".js")), f =>
        {
            Assert.DoesNotContain("innerHTML", f.Value);
            Assert.DoesNotContain("outerHTML", f.Value);
            Assert.DoesNotContain("insertAdjacentHTML", f.Value);
            Assert.DoesNotContain("document.write", f.Value);
        });
    }

    // AC5/AC6/AC9/AC12: the page script is wired to the form, the picker, the manager link and the API routes it needs.
    [Fact]
    public async Task Page_script_uses_the_expected_api_routes()
    {
        var html = await PageAsync();
        Assert.Contains("<script type=\"module\" src=\"js/employee.js\"></script>", html);

        var js = await GetTextAsync("/js/employee.js");
        Assert.Contains("'/api/me/leave-requests'", js);
        Assert.Contains("'/api/leave-requests', { method: 'POST'", js);
        Assert.Contains("person.isManager", js);
        // Every API field the server can name maps to a form control with an error slot.
        foreach (var (field, id) in new[] { ("firstDay", "first-day"), ("lastDay", "last-day"), ("type", "type"), ("reason", "reason") })
        {
            Assert.Contains($"{field}: '{id}'", js);
            Assert.Contains($"id=\"{id}-error\"", html);
        }
    }

    // AC10: keyboard focus is always visible.
    [Fact]
    public async Task Stylesheet_has_visible_focus_style()
    {
        var css = await GetTextAsync("/css/site.css");

        Assert.Matches(":focus-visible\\s*\\{[^}]*outline:\\s*3px solid", css);
    }
}
