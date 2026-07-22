using System.ComponentModel.DataAnnotations;
using KissakiSignup.Web.Domain;

namespace KissakiSignup.Web.Services;

public static class RegistrationPayloadValidator
{
    public static IReadOnlyList<RuleMessage> Validate(RegistrationPayload payload)
    {
        var hasNullEntries = NormalizeNestedValues(payload);
        var messages = new List<RuleMessage>();

        if (hasNullEntries)
        {
            messages.Add(new RuleMessage("invalid-registration-data", "The registration data contains invalid entries.", true));
        }

        ValidateClubAndContact(payload, messages);
        ValidateCollectionLimits(payload, messages);
        ValidateCompetitors(payload, messages);
        ValidateTeams(payload, messages);

        return messages;
    }

    private static void ValidateClubAndContact(RegistrationPayload payload, List<RuleMessage> messages)
    {
        RequireText(payload.Club.Name, "club-name-required", "Club name is required.", messages);
        RequireText(payload.Club.City, "club-city-required", "Club city is required.", messages);
        RequireText(payload.Contact.Name, "contact-name-required", "Contact name is required.", messages);
        RequireText(payload.Contact.Email, "contact-email-required", "Contact email is required.", messages);
        if (!string.IsNullOrWhiteSpace(payload.Contact.Email) && !new EmailAddressAttribute().IsValid(payload.Contact.Email))
        {
            messages.Add(new RuleMessage("contact-email-invalid", "Contact email must be a valid email address.", true));
        }

        ValidateLength(payload.Club.Name, RegistrationPayloadLimits.MaxNameLength, "Club name", messages);
        ValidateLength(payload.Club.City, RegistrationPayloadLimits.MaxCityLength, "Club city", messages);
        ValidateLength(payload.Club.Country, RegistrationPayloadLimits.MaxCountryLength, "Club country", messages);
        ValidateLength(payload.Club.Address, RegistrationPayloadLimits.MaxAddressLength, "Club address", messages);
        ValidateLength(payload.Club.Email, RegistrationPayloadLimits.MaxEmailLength, "Club email", messages);
        ValidateLength(payload.Club.Phone, RegistrationPayloadLimits.MaxPhoneLength, "Club phone", messages);
        ValidateLength(payload.Club.Web, RegistrationPayloadLimits.MaxWebLength, "Club website", messages);
        ValidateLength(payload.Contact.Name, RegistrationPayloadLimits.MaxNameLength, "Contact name", messages);
        ValidateLength(payload.Contact.Email, RegistrationPayloadLimits.MaxEmailLength, "Contact email", messages);
        ValidateLength(payload.Contact.Phone, RegistrationPayloadLimits.MaxPhoneLength, "Contact phone", messages);
        ValidateLength(payload.Contact.Notes, RegistrationPayloadLimits.MaxNotesLength, "Contact notes", messages);
    }

    private static void ValidateCollectionLimits(RegistrationPayload payload, List<RuleMessage> messages)
    {
        if (payload.Competitors.Count == 0)
        {
            messages.Add(new RuleMessage("competitor-required", "At least one competitor is required.", true));
        }

        if (payload.Competitors.Count > RegistrationPayloadLimits.MaxCompetitors)
        {
            messages.Add(new RuleMessage("competitor-limit", "A registration can contain no more than 100 competitors.", true));
        }

        if (payload.Teams.Count > RegistrationPayloadLimits.MaxTeams)
        {
            messages.Add(new RuleMessage("team-limit", "A registration can contain no more than 50 teams.", true));
        }
    }

    private static void ValidateCompetitors(RegistrationPayload payload, List<RuleMessage> messages)
    {
        foreach (var competitor in payload.Competitors)
        {
            RequireText(competitor.FirstName, "competitor-first-name-required", "Competitor first name is required.", messages);
            RequireText(competitor.LastName, "competitor-last-name-required", "Competitor last name is required.", messages);
            ValidateLength(competitor.ClientId, RegistrationPayloadLimits.MaxClientIdLength, "Competitor reference", messages);
            ValidateLength(competitor.FirstName, RegistrationPayloadLimits.MaxNameLength, "Competitor first name", messages);
            ValidateLength(competitor.LastName, RegistrationPayloadLimits.MaxNameLength, "Competitor last name", messages);
            ValidateLength(competitor.IdCard, RegistrationPayloadLimits.MaxIdCardLength, "Competitor id card", messages);
            ValidateLength(competitor.RankText, RegistrationPayloadLimits.MaxRankLength, "Competitor rank", messages);
            ValidateLength(competitor.Notes, RegistrationPayloadLimits.MaxNotesLength, "Competitor notes", messages);

            if (competitor.BirthYear < 1900 || competitor.BirthYear > DateTime.UtcNow.Year)
            {
                messages.Add(new RuleMessage("competitor-birth-year-invalid", $"Competitor birth year must be between 1900 and {DateTime.UtcNow.Year}.", true));
            }

            if (string.IsNullOrWhiteSpace(competitor.RankText) || TournamentRules.ClassifyRank(competitor.RankText) == RankKind.Unknown)
            {
                messages.Add(new RuleMessage("competitor-rank-invalid", "Competitor rank must include Kyu or Dan.", true));
            }

            if (competitor.Categories.Count > RegistrationPayloadLimits.MaxCategoriesPerCompetitor)
            {
                messages.Add(new RuleMessage("competitor-category-limit", "A competitor can contain no more than six categories.", true));
            }

            if (competitor.Categories.Any(category => !Enum.IsDefined(category)))
            {
                messages.Add(new RuleMessage("competition-category-invalid", "The selected competition category is invalid.", true));
            }

            messages.AddRange(TournamentRules.ValidateCompetitor(
                competitor.BirthYear,
                competitor.RankText,
                competitor.Categories.Where(Enum.IsDefined),
                competitor.IdCard));
        }

        var duplicateIdCards = payload.Competitors
            .Select(competitor => IdCardService.Normalize(competitor.IdCard))
            .Where(idCard => !string.IsNullOrEmpty(idCard))
            .GroupBy(idCard => idCard, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);
        if (duplicateIdCards.Any())
        {
            messages.Add(new RuleMessage("idcard-duplicate", "Each non-empty id card must be unique.", true));
        }
    }

    private static void ValidateTeams(RegistrationPayload payload, List<RuleMessage> messages)
    {
        var competitorsByClientId = payload.Competitors
            .GroupBy(competitor => Trim(competitor.ClientId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        foreach (var duplicateClientId in competitorsByClientId.Where(pair => !string.IsNullOrEmpty(pair.Key) && pair.Value.Count > 1))
        {
            messages.Add(new RuleMessage("competitor-reference-duplicate", "Competitor references must be unique when used by a team.", true));
        }

        var memberReferences = payload.Teams
            .SelectMany(team => team.Members)
            .Select(member => Trim(member.CompetitorClientId))
            .Where(reference => !string.IsNullOrEmpty(reference))
            .ToList();
        foreach (var reference in memberReferences)
        {
            if (!competitorsByClientId.TryGetValue(reference, out var matches) || matches.Count != 1)
            {
                messages.Add(new RuleMessage("team-member-unknown", "Each selected team member must reference exactly one competitor.", true));
            }
        }

        if (memberReferences.GroupBy(reference => reference, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            messages.Add(new RuleMessage("team-member-duplicate", "A competitor can only be selected once across team entries.", true));
        }

        var referencedCompetitors = memberReferences.ToHashSet(StringComparer.Ordinal);
        foreach (var competitor in payload.Competitors.Where(competitor => competitor.Categories.Count == 0 && !referencedCompetitors.Contains(Trim(competitor.ClientId))))
        {
            messages.Add(new RuleMessage("competitor-entry-required", "Each competitor must have a category or be selected for a team.", true));
        }

        foreach (var team in payload.Teams)
        {
            RequireText(team.Name, "team-name-required", "Team name is required.", messages);
            ValidateLength(team.Name, RegistrationPayloadLimits.MaxNameLength, "Team name", messages);

            var occupiedMembers = team.Members
                .Where(member => !string.IsNullOrWhiteSpace(member.CompetitorClientId))
                .ToList();
            if (occupiedMembers.Any(member => member.Position is < 1 or > 3))
            {
                messages.Add(new RuleMessage("team-member-position-invalid", "Each occupied team position must be between 1 and 3.", true));
            }

            if (occupiedMembers.GroupBy(member => member.Position).Any(group => group.Count() > 1))
            {
                messages.Add(new RuleMessage("team-member-position-duplicate", "Each occupied team position must be unique.", true));
            }

            if (!Enum.IsDefined(team.TeamType))
            {
                messages.Add(new RuleMessage("team-type-invalid", "The selected team type is invalid.", true));
                continue;
            }

            if (team.Members.Count > RegistrationPayloadLimits.MaxTeamMembers)
            {
                messages.Add(new RuleMessage("team-member-limit", "A team can contain no more than three members.", true));
            }

            var resolvedMembers = team.Members
                .Select(member => (Member: member, Reference: Trim(member.CompetitorClientId)))
                .Where(item => !string.IsNullOrEmpty(item.Reference))
                .Where(item => competitorsByClientId.TryGetValue(item.Reference, out var matches) && matches.Count == 1)
                .Select(item =>
                {
                    var competitor = competitorsByClientId[item.Reference].Single();
                    return new TeamMemberInput(item.Member.Position, competitor.BirthYear, competitor.RankText);
                });
            messages.AddRange(TournamentRules.ValidateTeam(team.TeamType, resolvedMembers));
        }
    }

    private static bool NormalizeNestedValues(RegistrationPayload payload)
    {
        var hasNullEntries = false;
        payload.Club ??= new ClubPayload();
        payload.Contact ??= new ContactPayload();
        payload.Competitors ??= [];
        payload.Teams ??= [];

        if (payload.Competitors.RemoveAll(competitor => competitor is null) > 0)
        {
            hasNullEntries = true;
        }

        if (payload.Teams.RemoveAll(team => team is null) > 0)
        {
            hasNullEntries = true;
        }

        foreach (var competitor in payload.Competitors)
        {
            competitor.Categories ??= [];
        }

        foreach (var team in payload.Teams)
        {
            team.Members ??= [];
            if (team.Members.RemoveAll(member => member is null) > 0)
            {
                hasNullEntries = true;
            }
        }

        return hasNullEntries;
    }

    private static void RequireText(string? value, string code, string text, List<RuleMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            messages.Add(new RuleMessage(code, text, true));
        }
    }

    private static void ValidateLength(string? value, int limit, string field, List<RuleMessage> messages)
    {
        if (value?.Length > limit)
        {
            messages.Add(new RuleMessage($"{field.ToLowerInvariant().Replace(' ', '-')}-too-long", $"{field} must not exceed {limit} characters.", true));
        }
    }

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
}
