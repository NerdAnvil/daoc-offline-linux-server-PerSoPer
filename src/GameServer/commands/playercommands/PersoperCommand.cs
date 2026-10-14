using System;
using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>
    /// Summons Persoper, the server's one persistent social personality. Her
    /// body is a normal temporary companion (existing combat AI, no loot, gear
    /// not saved); her name and look per realm are remembered between summons.
    /// </summary>
    [CmdAttribute("&persoper", ePrivLevel.Player, "Summons Persoper, a unique healer companion", "/persoper")]
    public sealed class PersoperCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        // outfit01, her signature Jem-style look: hot magenta body, violet trim,
        // turquoise cloak. Approved in game.
        // How each id looks depends on the armour material; check in game.
        private static readonly (eInventorySlot Slot, eColor Color)[] Outfit01 =
        [
            (eInventorySlot.TorsoArmor, eColor.Cloth_Purple_Red),
            (eInventorySlot.LegsArmor, eColor.Cloth_Purple_Red),
            (eInventorySlot.ArmsArmor, eColor.Cloth_Bright_Violet),
            (eInventorySlot.HandsArmor, eColor.Cloth_Bright_Violet),
            (eInventorySlot.FeetArmor, eColor.Cloth_Bright_Violet),
            (eInventorySlot.HeadArmor, eColor.Cloth_Bright_Violet),
            (eInventorySlot.Cloak, eColor.Cloth_Turquoise),
        ];

        private static GameBot _active;

        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            if (player == null)
                return;

            if (_active is { ObjectState: GameObject.eObjectState.Active } current)
            {
                DisplayMessage(client, ReferenceEquals(current.Owner, player)
                    ? $"{current.Name} is already at your side."
                    : $"{current.Name} is out adventuring with {current.Owner?.Name ?? "someone else"} right now.");
                return;
            }

            string conflict = PersoperStateStore.FindNameConflict();
            if (conflict != null)
            {
                DisplayMessage(client, $"Persoper refuses to appear while the character '{conflict}' uses her name.");
                return;
            }

            eCharacterClass characterClass = PersoperIdentity.HealerClassFor(player.Realm);
            PersoperStateRecord state = PersoperStateStore.Load();
            Random random = Random.Shared;

            eRace race = state.RaceFor(player.Realm);
            if (!AutonomousBotIdentityGenerator.GetEligibleRaces(characterClass).Contains(race))
            {
                var races = AutonomousBotIdentityGenerator.GetEligibleRaces(characterClass).ToArray();
                race = races.Length > 0 ? races[random.Next(races.Length)] : race;
            }

            bool toneDown = PersoperProperties.TONE_DOWN;
            bool firstMeeting = !PersoperIdentity.IsValidSurname(state.Surname);
            bool mustClean = toneDown && !PersoperIdentity.IsCleanSurname(state.Surname);
            bool newSurname = firstMeeting || mustClean ||
                PersoperIdentity.ShouldChangeSurname(PersoperProperties.SURNAME_CHANGE_PERCENT, random);
            string surname = newSurname
                ? PersoperIdentity.ChooseSurname(characterClass, race, toneDown, state.Surname, random)
                : state.Surname;

            GameBot persoper = SpawnTemporaryGroupBotCommandHandler.AddHelperToGroup(
                client, characterClass, PersoperIdentity.FullName(surname), race, eGender.Female, ApplyLook);
            if (persoper == null)
                return;

            _active = persoper;
            string now = PersoperStateStore.UtcNow();
            if (newSurname)
            {
                if (!firstMeeting)
                    state.SurnameChangeCount++;
                state.Surname = surname;
                state.SurnameChosenUtc = now;
            }
            state.SetRace(player.Realm, (eRace)persoper.Race);
            state.SummonCount++;
            state.LastSummonedBy = player.Name;
            state.LastSummonedUtc = now;
            if (!PersoperStateStore.Save(state))
                DisplayMessage(client, "Persoper could not write down her name this time; she may not remember it.");

            PersoperIdentity.eGreeting greeting = firstMeeting ? PersoperIdentity.eGreeting.FirstMeeting
                : newSurname ? PersoperIdentity.eGreeting.NewSurname
                : PersoperIdentity.eGreeting.Returning;
            persoper.Say(PersoperIdentity.Greeting(greeting, surname, player.Name, toneDown, random));
            string grumble = PersoperIdentity.OffClassGrumble(characterClass, random);
            if (grumble != null)
                persoper.Say(grumble);

            DisplayMessage(client,
                $"{persoper.Name}, level {persoper.Level} {characterClass}, joined your group. " +
                "Like other companions she takes no loot and her gear is not saved, but she remembers who she is.");
        }

        private static void ApplyLook(GameBot persoper)
        {
            if (persoper.Inventory == null)
                return;

            foreach ((eInventorySlot slot, eColor color) in Outfit01)
            {
                if (persoper.Inventory.GetItem(slot) is { } item)
                    item.Color = (int)color;
            }
        }
    }
}
