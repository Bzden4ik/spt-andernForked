using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Prestige;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace BarlogM_Andern;

[Injectable(InjectionType.Scoped, typeof(PrestigeController))]
class PrestigeControllerEx(
    ProfileHelper profileHelper,
    DatabaseService databaseService,
    SaveServer saveServer,
    ModData modData) :
    PrestigeController(profileHelper, databaseService, saveServer)
{
    private readonly ModConfig _modConfig = modData.ModConfig;

    public override async Task ObtainPrestige(MongoId sessionId, ObtainPrestigeRequestList request)
    {
        if (!_modConfig.CheesePrestige)
        {
            await base.ObtainPrestige(sessionId, request);
            return;
        }
        
        var profile = profileHelper.GetFullProfile(sessionId);
        if (profile is not null)
        {
            var pendingPrestige = new PendingPrestige
            {
                PrestigeLevel = (profile.CharacterData?.PmcData?.Info?.PrestigeLevel ?? 0) + 1,
                Items = request,
            };

            profile.SptData.PendingPrestige = pendingPrestige;
            profile.ProfileInfo.IsWiped = true;

            var prestigeLevels = databaseService.GetTemplates().Prestige?.Elements ?? [];

            var prestigeRewards = prestigeLevels
                .Slice(0, pendingPrestige.PrestigeLevel.Value)
                .SelectMany(prestigeInner => prestigeInner.Rewards);

            var customisationTemplateDb = databaseService.GetTemplates().Customization;

            foreach (var reward in prestigeRewards)
            {
                if (!MongoId.IsValidMongoId(reward.Target))
                {
                    continue;
                }

                if (!customisationTemplateDb.TryGetValue(reward.Target, out var template))
                {
                    continue;
                }

                // This has to be done before the profile is wiped, as the user can only select a new head during the wipe
                if (template.Parent == CustomisationTypeId.HEAD)
                {
                    profileHelper.AddHideoutCustomisationUnlock(profile, reward, CustomisationSource.PRESTIGE);
                }
            }

            await saveServer.SaveProfileAsync(sessionId);
        }
    }
}
