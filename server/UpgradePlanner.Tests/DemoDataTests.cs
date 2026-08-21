using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using UpgradePlanner.Api.Dtos;
using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Tests;

/// <summary>
/// Properties of the public demo fixture.
/// </summary>
/// <remarks>
/// The fixture ships to a public URL, so "it contains nothing personal" is a
/// <b>tested property</b> rather than a promise someone remembered to keep.
/// </remarks>
public class DemoDataTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static IEnumerable<string> AllText()
        => Api.Data.DemoData.Build(Now)
            .SelectMany(row => new[]
            {
                row.Entity.Title,
                row.Entity.Description,
                row.Entity.Notes,
                row.Entity.ProductLink,
            })
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!);

    // --- The guarantee that matters -----------------------------------------

    [Fact]
    public void The_personal_data_scan_actually_has_something_to_scan()
    {
        // Without this, every scan below would pass trivially if AllText() ever
        // stopped returning anything - a green suite guarding nothing.
        var text = AllText().ToList();

        Assert.True(text.Count >= 20, $"Expected a substantial body of fixture text, found {text.Count} strings.");
        Assert.Contains(text, t => t.Length > 100);
    }

    [Fact]
    public void The_email_scan_would_catch_an_email_if_one_appeared()
    {
        // Proves the pattern the scan relies on is not vacuous.
        var pattern = new Regex(@"[\w.+-]+@[\w-]+\.[\w.]+", RegexOptions.IgnoreCase);

        Assert.Matches(pattern, "somebody@example.com");
        Assert.Matches(pattern, "first.last+tag@mail.example.co.uk");
    }

    [Fact]
    public void The_fixture_contains_no_email_addresses()
    {
        var pattern = new Regex(@"[\w.+-]+@[\w-]+\.[\w.]+", RegexOptions.IgnoreCase);

        foreach (var text in AllText())
        {
            Assert.False(pattern.IsMatch(text), $"Fixture text looks like it contains an email address: {text}");
        }
    }

    [Fact]
    public void The_fixture_contains_no_phone_numbers()
    {
        // Seven or more digits in a row, or a grouped run with separators.
        var pattern = new Regex(@"(\+\d[\d\s().-]{7,})|(\d{7,})");

        foreach (var text in AllText())
        {
            Assert.False(pattern.IsMatch(text), $"Fixture text looks like it contains a phone number: {text}");
        }
    }

    [Fact]
    public void The_fixture_contains_no_credential_shaped_strings()
    {
        var patterns = new[]
        {
            new Regex(@"(?i)\b(api[_-]?key|secret|password|passwd|token|bearer)\b\s*[:=]"),
            new Regex(@"\b(gh[pousr]_[A-Za-z0-9]{16,})\b"),
            new Regex(@"\bsk-[A-Za-z0-9]{16,}\b"),
            new Regex(@"BEGIN [A-Z ]*PRIVATE KEY"),
        };

        foreach (var text in AllText())
        {
            foreach (var pattern in patterns)
            {
                Assert.False(pattern.IsMatch(text), $"Fixture text looks like it contains a credential: {text}");
            }
        }
    }

    [Fact]
    public void Every_url_in_the_fixture_points_at_a_reserved_example_domain()
    {
        // A fixture link must be self-evidently fake. example.com and example.org
        // are reserved by RFC 2606 for exactly this and resolve to nothing real,
        // so a reviewer clicking one cannot end up anywhere the author has been.
        var urls = new Regex(@"https?://[^\s""'<>]+");

        foreach (var text in AllText())
        {
            foreach (Match match in urls.Matches(text))
            {
                var host = new Uri(match.Value).Host;
                Assert.True(
                    host is "example.com" or "example.org" or "www.example.com",
                    $"Fixture contains a link to a non-example host: {host}");
            }
        }
    }

    // --- The fixture has to be worth looking at ------------------------------

    [Fact]
    public void Every_category_appears_at_least_once()
    {
        var present = Api.Data.DemoData.Build(Now).Select(r => r.Entity.Category).ToHashSet();

        foreach (var category in Enum.GetValues<UpgradeCategory>())
        {
            Assert.Contains(category, present);
        }
    }

    [Fact]
    public void Every_status_appears_at_least_once()
    {
        var present = Api.Data.DemoData.Build(Now).Select(r => r.Entity.Status).ToHashSet();

        foreach (var status in Enum.GetValues<UpgradeStatus>())
        {
            Assert.Contains(status, present);
        }
    }

    [Fact]
    public void The_fixture_demonstrates_a_multi_step_prerequisite_chain()
    {
        // A single parent-child pair does not show priority propagating; a chain does.
        var rows = Api.Data.DemoData.Build(Now);
        var byKey = rows.ToDictionary(r => r.Key);

        var deepest = rows.Max(r =>
        {
            var depth = 0;
            var current = r;
            while (current.PrerequisiteKey is { } key)
            {
                depth++;
                current = byKey[key];
            }
            return depth;
        });

        Assert.True(deepest >= 2, $"Expected a chain at least 2 links deep, found {deepest}.");
    }

    [Fact]
    public void The_fixture_covers_the_priority_boundaries()
    {
        var priorities = Api.Data.DemoData.Build(Now).Select(r => r.Entity.Priority).ToList();

        Assert.Contains(1, priorities);
        Assert.Contains(100, priorities);
    }

    [Fact]
    public void The_fixture_includes_an_upgrade_with_no_cost_recorded()
    {
        Assert.Contains(Api.Data.DemoData.Build(Now), r => r.Entity.EstimatedCost is null);
    }

    [Fact]
    public void The_fixture_includes_a_minimal_record_with_only_required_fields()
    {
        Assert.Contains(Api.Data.DemoData.Build(Now), r =>
            r.Entity.Description is null
            && r.Entity.Notes is null
            && r.Entity.ProductLink is null
            && r.Entity.EstimatedCost is null
            && r.Entity.PrerequisiteUpgradeId is null);
    }

    [Fact]
    public void The_fixture_includes_a_long_title_to_exercise_the_layout()
    {
        Assert.Contains(Api.Data.DemoData.Build(Now), r => r.Entity.Title.Length > 60);
    }

    // --- It must not rot -----------------------------------------------------

    [Fact]
    public void Every_date_is_relative_to_seed_time()
    {
        // A fixture pinned to absolute dates shows dead streaks and empty
        // "this month" stats a year later.
        var early = Api.Data.DemoData.Build(Now);
        var later = Api.Data.DemoData.Build(Now.AddYears(3));
        var shift = TimeSpan.FromDays(365 * 3 + 1); // leap days make this approximate

        foreach (var (a, b) in early.Zip(later))
        {
            Assert.True(
                b.Entity.CreatedAt - a.Entity.CreatedAt >= shift,
                $"'{a.Entity.Title}' has a CreatedAt that did not move with seed time.");

            if (a.Entity.PurchasedDate is { } aPurchased && b.Entity.PurchasedDate is { } bPurchased)
            {
                Assert.True(bPurchased - aPurchased >= shift,
                    $"'{a.Entity.Title}' has a PurchasedDate that did not move with seed time.");
            }
        }
    }

    [Fact]
    public void The_fixture_is_deterministic_for_a_given_now()
    {
        var first = Api.Data.DemoData.Build(Now);
        var second = Api.Data.DemoData.Build(Now);

        Assert.Equal(
            first.Select(r => (r.Key, r.Entity.Title, r.Entity.Priority, r.Entity.CreatedAt)),
            second.Select(r => (r.Key, r.Entity.Title, r.Entity.Priority, r.Entity.CreatedAt)));
    }

    [Fact]
    public void No_fixture_date_is_in_the_future()
    {
        foreach (var row in Api.Data.DemoData.Build(Now))
        {
            Assert.True(row.Entity.CreatedAt <= Now, $"'{row.Entity.Title}' was created in the future.");
            Assert.True(row.Entity.UpdatedAt <= Now, $"'{row.Entity.Title}' was updated in the future.");
            Assert.True(row.Entity.PurchasedDate is null || row.Entity.PurchasedDate <= Now,
                $"'{row.Entity.Title}' was purchased in the future.");
        }
    }

    // --- Internal consistency ------------------------------------------------

    [Fact]
    public void Every_fixture_row_would_pass_the_public_api_validation()
    {
        // The demo cannot contain data a user would be forbidden from entering.
        foreach (var row in Api.Data.DemoData.Build(Now))
        {
            var dto = new UpsertUpgradeDto
            {
                Title = row.Entity.Title,
                Description = row.Entity.Description,
                Category = row.Entity.Category,
                Priority = row.Entity.Priority,
                EstimatedCost = row.Entity.EstimatedCost,
                Status = row.Entity.Status,
                Notes = row.Entity.Notes,
                ProductLink = row.Entity.ProductLink,
                PurchasedDate = row.Entity.PurchasedDate,
                ActualCost = row.Entity.ActualCost,
            };

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

            Assert.True(results.Count == 0,
                $"Fixture row '{row.Entity.Title}' would be rejected by the API: "
                + string.Join("; ", results.Select(r => r.ErrorMessage)));
        }
    }

    [Fact]
    public void Every_prerequisite_key_is_defined_before_it_is_referenced()
    {
        // The seeder inserts in list order, so a forward reference would produce a
        // null link rather than a chain.
        var seen = new HashSet<string>();

        foreach (var row in Api.Data.DemoData.Build(Now))
        {
            if (row.PrerequisiteKey is { } key)
            {
                Assert.True(seen.Contains(key),
                    $"Row '{row.Key}' references prerequisite '{key}' before it is defined.");
            }
            seen.Add(row.Key);
        }
    }

    [Fact]
    public void Fixture_keys_are_unique()
    {
        var rows = Api.Data.DemoData.Build(Now);

        Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct().Count());
    }
}
