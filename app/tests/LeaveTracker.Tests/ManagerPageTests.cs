using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    // The page script is served with a JavaScript type (ES modules need it).
    [Fact]
    public async Task Manager_script_is_served_as_javascript()
    {
        var response = await _client.GetAsync("/js/manager.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    // AC2: the page reuses the employee page's picker and remembered choice, so both pages pick the same person.
    [Fact]
    public async Task Manager_script_shares_the_remembered_choice()
    {
        var html = await PageAsync();
        Assert.Contains("<script type=\"module\" src=\"js/manager.js\"></script>", html);

        var js = await GetTextAsync("/js/manager.js");
        Assert.Contains("from './identity.js'", js);
        Assert.Contains("loadPicker(who)", js);
        Assert.Contains("rememberEmployeeId(person ? person.id : null)", js);
        Assert.Contains("person.isManager", js);
        // No second storage key: only identity.js touches localStorage.
        Assert.DoesNotContain("localStorage", js);
    }

    // AC7/AC8/AC9/AC10: the script calls the team list and decision routes, maps field errors, and refreshes on conflicts.
    [Fact]
    public async Task Page_script_uses_the_expected_api_routes()
    {
        var js = await GetTextAsync("/js/manager.js");

        Assert.Contains("'/api/team/leave-requests/pending'", js);
        Assert.Contains("`/api/leave-requests/${panel.request.id}/${action}`", js);
        Assert.Contains("panel.approve ? 'approve' : 'reject'", js);
        Assert.Contains("panel.approve ? { comment: text } : { reason: text }", js);
        Assert.Contains("fieldErrors.reason ?? fieldErrors.comment", js);
        Assert.Contains("[403, 404, 409].includes(error.status)", js);
        Assert.Contains("await loadTeam()", js);
        // Double-send guard and cancel by Escape.
        Assert.Contains("if (!panel || sending || !current)", js);
        Assert.Contains("panel.confirm.disabled = true", js);
        Assert.Contains("event.key === 'Escape'", js);
    }

    // AC12: decisions are told apart by words, not colour.
    [Fact]
    public async Task Decide_buttons_are_words()
    {
        var js = await GetTextAsync("/js/manager.js");
        var panel = DecisionTemplate(await PageAsync());

        Assert.Contains("decideButton('Approve'", js);
        Assert.Contains("decideButton('Reject'", js);
        Assert.Contains("'Confirm rejection'", js);
        Assert.Contains("Confirm approval", panel);
        Assert.Contains("Cancel", panel);
        Assert.Contains("'Nothing is waiting for your decision.'", js);
        Assert.Contains("waiting for your decision`", js);
    }

    // Safety: nothing is loaded from the internet, and names and reasons are never inserted as HTML.
    [Fact]
    public async Task Manager_page_uses_no_external_urls_or_innerHTML()
    {
        var html = await PageAsync();
        var js = await GetTextAsync("/js/manager.js");

        Assert.DoesNotMatch("https?://", html);
        Assert.DoesNotMatch("https?://", js);
        Assert.DoesNotContain("innerHTML", js);
        Assert.DoesNotContain("outerHTML", js);
        Assert.DoesNotContain("insertAdjacentHTML", js);
        Assert.DoesNotContain("document.write", js);
    }

    private const string Maya = "maya.manager@example.com";
    private const string Alex = "alex.turner@example.com";

    private static async Task<int> AlexAsksForTripAsync(TestAppFactory factory)
    {
        using var alex = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        var response = await alex.PostAsJsonAsync(
            "/api/leave-requests", new { firstDay = "2026-12-14", lastDay = "2026-12-16", type = "annual", reason = "Trip" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<List<JsonElement>> PendingForMayaAsync(HttpClient maya)
    {
        var response = await maya.GetAsync("/api/team/leave-requests/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    // AC6/AC7: what the page lists for Maya, and that approving takes the request off the list (the count goes down).
    [Fact]
    public async Task Approving_removes_the_request_from_the_team_list()
    {
        using var factory = new TestAppFactory();
        var requestId = await AlexAsksForTripAsync(factory);
        using var maya = factory.CreateClientAs(await factory.EmployeeIdAsync(Maya));

        var before = await PendingForMayaAsync(maya);
        var row = Assert.Single(before, r => r.GetProperty("id").GetInt32() == requestId);
        Assert.Equal("Alex Turner", row.GetProperty("employeeName").GetString());
        Assert.Equal("2026-12-14", row.GetProperty("firstDay").GetString());
        Assert.Equal("2026-12-16", row.GetProperty("lastDay").GetString());
        Assert.Equal("annual", row.GetProperty("type").GetString());
        Assert.Equal(3, row.GetProperty("daysCovered").GetInt32());
        Assert.Equal("Trip", row.GetProperty("reason").GetString());

        var approve = await maya.PostAsJsonAsync($"/api/leave-requests/{requestId}/approve", new { comment = "Enjoy" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var after = await PendingForMayaAsync(maya);
        Assert.DoesNotContain(after, r => r.GetProperty("id").GetInt32() == requestId);
        Assert.Equal(before.Count - 1, after.Count);

        // Alex sees it as approved, with the comment, on the employee page.
        using var alex = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        using var mine = JsonDocument.Parse(await alex.GetStringAsync("/api/me/leave-requests"));
        var decided = mine.RootElement.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == requestId);
        Assert.Equal("approved", decided.GetProperty("status").GetString());
        Assert.Equal("Enjoy", decided.GetProperty("decisionComment").GetString());
    }

    // AC8: a rejection without a reason is refused with a message for the reason field and stays listed; with a reason it goes.
    [Fact]
    public async Task Rejecting_needs_a_reason_and_then_removes_the_request()
    {
        using var factory = new TestAppFactory();
        var requestId = await AlexAsksForTripAsync(factory);
        using var maya = factory.CreateClientAs(await factory.EmployeeIdAsync(Maya));

        var blank = await maya.PostAsJsonAsync($"/api/leave-requests/{requestId}/reject", new { reason = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        using (var problem = JsonDocument.Parse(await blank.Content.ReadAsStringAsync()))
        {
            var message = problem.RootElement.GetProperty("errors").GetProperty("reason")[0].GetString();
            Assert.Contains("A rejection needs a reason", message);
        }
        Assert.Contains(await PendingForMayaAsync(maya), r => r.GetProperty("id").GetInt32() == requestId);

        var reject = await maya.PostAsJsonAsync($"/api/leave-requests/{requestId}/reject", new { reason = "Release week" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        Assert.DoesNotContain(await PendingForMayaAsync(maya), r => r.GetProperty("id").GetInt32() == requestId);
    }

    // AC9: deciding a request that was already decided elsewhere gives the "already decided" message the page shows.
    [Fact]
    public async Task Deciding_twice_gives_the_already_decided_message()
    {
        using var factory = new TestAppFactory();
        var requestId = await AlexAsksForTripAsync(factory);
        using var maya = factory.CreateClientAs(await factory.EmployeeIdAsync(Maya));
        Assert.Equal(HttpStatusCode.OK, (await maya.PostAsJsonAsync($"/api/leave-requests/{requestId}/approve", new { comment = (string?)null })).StatusCode);

        var again = await maya.PostAsJsonAsync($"/api/leave-requests/{requestId}/reject", new { reason = "Changed my mind" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using var problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
        Assert.Contains("already approved by Maya Manager", problem.RootElement.GetProperty("detail").GetString());
    }
}
