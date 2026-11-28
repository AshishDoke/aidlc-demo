using System.Net;
using System.Text.RegularExpressions;

namespace LeaveTracker.Tests;

/// <summary>
/// LT-004: the manager page is served where the employee page links, and its markup carries what keyboard and
/// screen-reader users need. Interactive behaviour is covered by the manual checklist in the test report.
/// </summary>
public class ManagerPageTests : IClassFixture<ManagerPageTests.Host>
{
    public sealed class Host : IDisposable
    {
        public TestAppFactory Factory { get; } = new();

        public void Dispose() => Factory.Dispose();
    }

    private readonly HttpClient _client;

    public ManagerPageTests(Host host) => _client = host.Factory.CreateClient();

    private async Task<string> GetTextAsync(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private Task<string> PageAsync() => GetTextAsync("/manager.html");

    /// <summary>Returns the opening tag of the element with the given id.</summary>
    private static string TagWithId(string html, string id)
    {
        var match = Regex.Match(html, $"<[a-z0-9]+[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Element with id '{id}' not found on the page.");
        return match.Value;
    }

    /// <summary>The markup inside the decision panel template.</summary>
    private static string DecisionTemplate(string html)
    {
        var match = Regex.Match(html, "<template id=\"decision-template\">(.*?)</template>", RegexOptions.Singleline);
        Assert.True(match.Success, "The decision panel template was not found.");
        return match.Groups[1].Value;
    }

    // AC1: the employee page's manager link now leads to a real page.
    [Fact]
    public async Task Manager_page_is_served_where_the_employee_page_links()
    {
        var employeePage = await GetTextAsync("/");
        var href = Regex.Match(TagWithId(employeePage, "manager-link"), "href=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal("manager.html", href);

        var response = await _client.GetAsync("/" + href);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>Leave Tracker – Manager page</title>", html);
    }

    // AC1/AC4: there is always a way back to the employee page.
    [Fact]
    public async Task Manager_page_links_back_to_the_employee_page()
    {
        var html = await PageAsync();

        var link = TagWithId(html, "employee-link");
        Assert.Contains("href=\"index.html\"", link);
        Assert.DoesNotContain(" hidden", link);
        Assert.Contains(">Employee page</a>", html);
    }

    // AC2: the same "who are you" picker as the employee page, at the top.
    [Fact]
    public async Task Manager_page_has_the_shared_picker()
    {
        var html = await PageAsync();

        Assert.Contains("<label for=\"who\">Who are you?</label>", html);
        Assert.Matches("<select id=\"who\"[^>]*>\\s*<option value=\"\">— Pick who you are —</option>", html);
        Assert.True(html.IndexOf("id=\"who\"", StringComparison.Ordinal) < html.IndexOf("<main", StringComparison.Ordinal));
        Assert.Contains("Demo: there is no login.", html);
    }

    // AC3: with nobody picked, the page asks to pick, and no list is shown.
    [Fact]
    public async Task Manager_page_shows_pick_first_and_hides_team_section_by_default()
    {
        var html = await PageAsync();

        Assert.DoesNotContain(" hidden", TagWithId(html, "pick-first"));
        Assert.Contains("Pick who you are to see the requests waiting for you.", html);
        Assert.Contains(" hidden", TagWithId(html, "team-section"));
        Assert.Contains(" hidden", TagWithId(html, "team-table"));
    }

    // AC4: a friendly message for non-managers exists, is hidden until needed, and points to the employee page.
    [Fact]
    public async Task Not_manager_message_is_present_and_hidden_by_default()
    {
        var html = await PageAsync();

        Assert.Contains(" hidden", TagWithId(html, "not-manager"));
        TagWithId(html, "not-manager-name");
        var message = Regex.Match(html, "<div id=\"not-manager\"[^>]*>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains("This page is for managers.", message);
        Assert.Contains("is not a manager.", message);
        Assert.Contains("<a href=\"index.html\">employee page</a>", message);
    }

    // AC5/AC6: the count heading sits above a table with every column the requirement lists.
    [Fact]
    public async Task Team_table_has_all_columns()
    {
        var html = await PageAsync();

        var count = TagWithId(html, "waiting-count");
        Assert.StartsWith("<h2 ", count);
        Assert.Contains("tabindex=\"-1\"", count);
        Assert.True(html.IndexOf("id=\"waiting-count\"", StringComparison.Ordinal) < html.IndexOf("id=\"team-table\"", StringComparison.Ordinal));

        Assert.Contains("<caption", html);
        var headers = Regex.Matches(html, "<th scope=\"col\"[^>]*>([^<]+)</th>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "Employee", "First day", "Last day", "Type", "Days", "Reason", "Decide" }, headers);
    }

    // AC8/AC11: the decision panel has a labelled text box linked to its hint and error slot, plus confirm and cancel.
    [Fact]
    public async Task Decision_panel_has_labelled_text_and_error_slot()
    {
        var panel = DecisionTemplate(await PageAsync());

        Assert.Contains("<label for=\"decision-text\">", panel);
        var textarea = TagWithId(panel, "decision-text");
        Assert.StartsWith("<textarea ", textarea);
        Assert.Contains("maxlength=\"500\"", textarea);
        Assert.Contains("aria-describedby=\"decision-hint decision-error\"", textarea);
        TagWithId(panel, "decision-hint");
        Assert.Contains(" hidden", TagWithId(panel, "decision-error"));
        Assert.Matches("<button type=\"submit\"[^>]*>Confirm approval</button>", panel);
        Assert.Matches("<button type=\"button\"[^>]*>Cancel</button>", panel);
    }

    // AC7/AC9/AC11: confirmations are announced politely and refusals as alerts.
    [Fact]
    public async Task Page_has_status_and_alert_regions()
    {
        var html = await PageAsync();

        var status = TagWithId(html, "page-status");
        Assert.Contains("role=\"status\"", status);
        Assert.Contains("aria-live=\"polite\"", status);
        Assert.Contains("role=\"alert\"", TagWithId(html, "page-error"));
        Assert.Contains("role=\"alert\"", TagWithId(html, "load-error"));
    }

    // AC11: every input, select and textarea (including the template's) has a matching label.
    [Fact]
    public async Task Every_input_has_a_label()
    {
        var html = await PageAsync();

        var controlIds = Regex.Matches(html, "<(input|select|textarea)\\b[^>]*\\bid=\"([^\"]+)\"", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[2].Value)
            .ToList();
        Assert.Equal(new[] { "who", "decision-text" }, controlIds);
        Assert.All(controlIds, id => Assert.Contains($"<label for=\"{id}\">", html));
    }

    // AC12: the manager page styles keep the outlined button readable as a word, and the focus outline still applies.
    [Fact]
    public async Task Stylesheet_has_manager_styles_and_visible_focus()
    {
        var css = await GetTextAsync("/css/site.css");

        Assert.Matches("\\.button-secondary\\s*\\{[^}]*border:\\s*2px solid", css);
        Assert.Matches("\\.decision-row\\s*>\\s*td\\s*\\{", css);
        Assert.Matches(":focus-visible\\s*\\{[^}]*outline:\\s*3px solid", css);
    }
}
