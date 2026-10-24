using System.Net;
using Microsoft.EntityFrameworkCore;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Fridges;

/// <summary>Fridge CRUD, members and invite links (API.md §6, PROJECT §8.1).</summary>
public sealed class FridgeEndpointsTests(ApiFactory factory) : FridgeTestBase(factory)
{
    [Fact]
    public async Task ListFridges_NewUser_HasTheDefaultFridgeAsOwner()
    {
        using var anna = await LoginAsync("anna");

        var list = await IsJsonArrayAsync(await anna.GetAsync("/api/v1/fridges"), "FridgeSummary");

        var fridge = list.EnumerateArray().ShouldHaveSingleItem();
        fridge.GetProperty("name").GetString().ShouldBe("Moja lodówka");
        fridge.GetProperty("role").GetString().ShouldBe("owner");
        fridge.GetProperty("owner").GetProperty("nickname").GetString().ShouldBe("anna");
        fridge.GetProperty("memberCount").GetInt32().ShouldBe(1);
        fridge.GetProperty("itemCount").GetInt32().ShouldBe(0);
        fridge.GetProperty("id").GetGuid().ShouldBe(await DefaultFridgeAsync(anna));
    }

    [Fact]
    public async Task CreateRenameDelete_Owner_Works()
    {
        using var anna = await LoginAsync("anna");

        var response = await anna.PostJsonAsync("/api/v1/fridges", new { name = "  Działka  " });
        var created = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "FridgeSummary");
        response.Headers.Location.ShouldNotBeNull();
        created.GetProperty("name").GetString().ShouldBe("Działka");
        var id = created.GetProperty("id").GetGuid();

        var renamed = await ContractAssert.IsJsonAsync(
            await anna.PutJsonAsync($"/api/v1/fridges/{id}", new { name = "Piwnica" }), HttpStatusCode.OK, "FridgeSummary");
        renamed.GetProperty("name").GetString().ShouldBe("Piwnica");

        var detail = await ContractAssert.IsJsonAsync(await anna.GetAsync($"/api/v1/fridges/{id}"), HttpStatusCode.OK, "FridgeDetail");
        detail.GetProperty("members").EnumerateArray().ShouldHaveSingleItem().GetProperty("role").GetString().ShouldBe("owner");

        (await anna.DeleteAsync($"/api/v1/fridges/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.IsProblemAsync(await anna.GetAsync($"/api/v1/fridges/{id}"), HttpStatusCode.NotFound, "fridge.not_found");
    }

    [Fact]
    public async Task CreateFridge_BlankName_Returns400()
    {
        using var anna = await LoginAsync("anna");

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync("/api/v1/fridges", new { name = "   " }), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task DeleteFridge_ItWasTheDefault_ResetsDefaultToAnotherFridge()
    {
        using var anna = await LoginAsync("anna");
        var first = await DefaultFridgeAsync(anna);
        var second = (await ApiClient.ReadJsonAsync(await anna.PostJsonAsync("/api/v1/fridges", new { name = "Druga" }))).GetProperty("id").GetGuid();

        (await anna.DeleteAsync($"/api/v1/fridges/{first}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await DefaultFridgeAsync(anna)).ShouldBe(second);
    }

    [Fact]
    public async Task AcceptInvite_SameUserConcurrently_AllSucceedWithOneMembership()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);
        var token = (await ApiClient.ReadJsonAsync(await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null))).GetProperty("token").GetString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => bartek.PostJsonAsync($"/api/v1/invites/{token}/accept", null)));

        foreach (var response in responses)
        {
            await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "FridgeSummary");
        }

        await using var db = Factory.CreateDbContext();
        (await db.FridgeMembers.CountAsync(m => m.FridgeId == fridgeId && m.UserId == bartek.UserId, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task InviteFlow_TwoUsers_JoinsAndIsIdempotent()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);

        var response = await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null);
        var invite = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "Invite");
        var token = invite.GetProperty("token").GetString()!;
        token.Length.ShouldBe(43);
        token.ShouldMatch("^[A-Za-z0-9_-]+$");
        invite.GetProperty("url").GetString().ShouldBe($"/zaproszenie/{token}");
        var expiresAt = invite.GetProperty("expiresAt").GetDateTimeOffset();
        (expiresAt - invite.GetProperty("createdAt").GetDateTimeOffset()).ShouldBe(TimeSpan.FromDays(7));

        var listed = await IsJsonArrayAsync(await anna.GetAsync($"/api/v1/fridges/{fridgeId}/invites"), "Invite");
        listed.EnumerateArray().ShouldHaveSingleItem().GetProperty("token").GetString().ShouldBe(token);

        var preview = await ContractAssert.IsJsonAsync(await bartek.GetAsync($"/api/v1/invites/{token}"), HttpStatusCode.OK, "InvitePreview");
        preview.GetProperty("fridgeName").GetString().ShouldBe("Moja lodówka");
        preview.GetProperty("owner").GetProperty("nickname").GetString().ShouldBe("anna");
        preview.GetProperty("alreadyMember").GetBoolean().ShouldBeFalse();

        var joined = await ContractAssert.IsJsonAsync(await bartek.PostJsonAsync($"/api/v1/invites/{token}/accept", null), HttpStatusCode.OK, "FridgeSummary");
        joined.GetProperty("id").GetGuid().ShouldBe(fridgeId);
        joined.GetProperty("role").GetString().ShouldBe("member");
        joined.GetProperty("memberCount").GetInt32().ShouldBe(2);

        var again = await ContractAssert.IsJsonAsync(await bartek.PostJsonAsync($"/api/v1/invites/{token}/accept", null), HttpStatusCode.OK, "FridgeSummary");
        again.GetProperty("memberCount").GetInt32().ShouldBe(2);

        var previewAfter = await ApiClient.ReadJsonAsync(await bartek.GetAsync($"/api/v1/invites/{token}"));
        previewAfter.GetProperty("alreadyMember").GetBoolean().ShouldBeTrue();

        var bartekFridges = await IsJsonArrayAsync(await bartek.GetAsync("/api/v1/fridges"), "FridgeSummary");
        bartekFridges.GetArrayLength().ShouldBe(2);

        var detail = await ContractAssert.IsJsonAsync(await bartek.GetAsync($"/api/v1/fridges/{fridgeId}"), HttpStatusCode.OK, "FridgeDetail");
        detail.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("nickname").GetString()).ShouldBe(["anna", "bartek"]);
        detail.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("role").GetString()).ShouldBe(["owner", "member"]);
    }

    [Fact]
    public async Task Invite_Revoked_Returns404OnPreviewAndAccept()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);
        var invite = await ApiClient.ReadJsonAsync(await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null));
        var token = invite.GetProperty("token").GetString();

        (await anna.DeleteAsync($"/api/v1/fridges/{fridgeId}/invites/{invite.GetProperty("id").GetGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ContractAssert.IsProblemAsync(await bartek.GetAsync($"/api/v1/invites/{token}"), HttpStatusCode.NotFound, "invite.not_found");
        await ContractAssert.IsProblemAsync(await bartek.PostJsonAsync($"/api/v1/invites/{token}/accept", null), HttpStatusCode.NotFound, "invite.not_found");
        (await IsJsonArrayAsync(await anna.GetAsync($"/api/v1/fridges/{fridgeId}/invites"), "Invite")).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Invite_Expired_Returns404OnPreviewAndAccept()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);
        var invite = await ApiClient.ReadJsonAsync(await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null));
        var token = invite.GetProperty("token").GetString();
        await using (var db = Factory.CreateDbContext())
        {
            var row = await db.FridgeInvites.SingleAsync(TestContext.Current.CancellationToken);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ContractAssert.IsProblemAsync(await bartek.GetAsync($"/api/v1/invites/{token}"), HttpStatusCode.NotFound, "invite.not_found");
        await ContractAssert.IsProblemAsync(await bartek.PostJsonAsync($"/api/v1/invites/{token}/accept", null), HttpStatusCode.NotFound, "invite.not_found");
        await ContractAssert.IsProblemAsync(await bartek.GetAsync("/api/v1/invites/unknown-token"), HttpStatusCode.NotFound, "invite.not_found");
    }

    [Fact]
    public async Task OwnerOnlyRoutes_Member_Returns403()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await JoinAsync(anna, bartek);

        await ContractAssert.IsProblemAsync(await bartek.PutJsonAsync($"/api/v1/fridges/{fridgeId}", new { name = "Moja" }), HttpStatusCode.Forbidden, "fridge.owner_required");
        await ContractAssert.IsProblemAsync(await bartek.DeleteAsync($"/api/v1/fridges/{fridgeId}"), HttpStatusCode.Forbidden, "fridge.owner_required");
        await ContractAssert.IsProblemAsync(await bartek.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null), HttpStatusCode.Forbidden, "fridge.owner_required");
        await ContractAssert.IsProblemAsync(await bartek.GetAsync($"/api/v1/fridges/{fridgeId}/invites"), HttpStatusCode.Forbidden, "fridge.owner_required");
        await ContractAssert.IsProblemAsync(await bartek.DeleteAsync($"/api/v1/fridges/{fridgeId}/members/{anna.UserId}"), HttpStatusCode.Forbidden, "fridge.owner_required");
    }

    [Fact]
    public async Task RemoveMember_OwnerLeaves_Returns400()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        await ContractAssert.IsProblemAsync(
            await anna.DeleteAsync($"/api/v1/fridges/{fridgeId}/members/{anna.UserId}"), HttpStatusCode.BadRequest, "fridge.owner_cannot_leave");
    }

    [Fact]
    public async Task RemoveMember_MemberLeaves_LosesAccess()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await JoinAsync(anna, bartek);

        (await bartek.DeleteAsync($"/api/v1/fridges/{fridgeId}/members/{bartek.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ContractAssert.IsProblemAsync(await bartek.GetAsync($"/api/v1/fridges/{fridgeId}"), HttpStatusCode.NotFound, "fridge.not_found");
        var detail = await ApiClient.ReadJsonAsync(await anna.GetAsync($"/api/v1/fridges/{fridgeId}"));
        detail.GetProperty("memberCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task RemoveMember_ItWasTheirDefault_ResetsToTheirRemainingFridgeOrNull()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await JoinAsync(anna, bartek);
        var bartekOwn = await DefaultFridgeAsync(bartek);
        await SetDefaultFridgeAsync(bartek.UserId, fridgeId);

        (await anna.DeleteAsync($"/api/v1/fridges/{fridgeId}/members/{bartek.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DefaultFridgeOrNullAsync(bartek)).ShouldBe(bartekOwn);

        // Without any other fridge the default becomes null.
        (await bartek.DeleteAsync($"/api/v1/fridges/{bartekOwn}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DefaultFridgeOrNullAsync(bartek)).ShouldBeNull();
        await JoinAsync(anna, bartek);
        await SetDefaultFridgeAsync(bartek.UserId, fridgeId);
        (await bartek.DeleteAsync($"/api/v1/fridges/{fridgeId}/members/{bartek.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DefaultFridgeOrNullAsync(bartek)).ShouldBeNull();
    }

    [Fact]
    public async Task AcceptInvite_UserWithoutDefault_AdoptsTheJoinedFridge()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        (await bartek.DeleteAsync($"/api/v1/fridges/{await DefaultFridgeAsync(bartek)}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var fridgeId = await JoinAsync(anna, bartek);

        (await DefaultFridgeOrNullAsync(bartek)).ShouldBe(fridgeId);
    }

    [Fact]
    public async Task RemoveMember_NotAMember_IsANoOp()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);

        (await anna.DeleteAsync($"/api/v1/fridges/{fridgeId}/members/{bartek.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>Invites <paramref name="member"/> into <paramref name="owner"/>'s default fridge and returns its id.</summary>
    private static async Task<Guid> JoinAsync(ApiClient owner, ApiClient member)
    {
        var fridgeId = await DefaultFridgeOrNullAsync(owner) ?? throw new InvalidOperationException();
        var invite = await ApiClient.ReadJsonAsync(await owner.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null));
        var response = await member.PostJsonAsync($"/api/v1/invites/{invite.GetProperty("token").GetString()}/accept", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return fridgeId;
    }

    private async Task SetDefaultFridgeAsync(Guid userId, Guid fridgeId)
    {
        await using var db = Factory.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
        user.DefaultFridgeId = fridgeId;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
