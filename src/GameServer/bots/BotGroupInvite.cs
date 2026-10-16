using System;

namespace DOL.GS;

public static class BotGroupInvite
{
    /// <param name="countQuickRecall">False when the bot rejoins by itself (making up), so it is not a re-invite.</param>
    public static bool TryInvite(GamePlayer player, GameBot bot, bool countQuickRecall = true)
    {
        if (player == null || bot == null || player.Realm != bot.Realm)
        {
            if (player != null)
                ChatUtil.SendSystemMessage(player, "Only a bot of your realm will accept the invitation.");
            return false;
        }

        // A personality bot that quit on this player sulks and turns the invite down.
        if (BotPersonalities.For(bot) is { Patience: { } patience } personality &&
            PersonalityAnnoyance.SulkLeft(personality, player) is var sulk && sulk > TimeSpan.Zero)
        {
            ChatUtil.SendSystemMessage(player, PersonalityAnnoyance.SulkRefusal(patience, player.Name, sulk, Random.Shared));
            return false;
        }

        if (player.Group != null && player.Group.Leader != player)
        {
            ChatUtil.SendSystemMessage(player, "You are not the leader of your group.");
            return false;
        }

        if (player.Group != null && player.Group.MemberCount >= ServerProperties.Properties.GROUP_MAX_MEMBER)
        {
            ChatUtil.SendSystemMessage(player, "The group is full.");
            return false;
        }

        // A Loner-type bot usually turns invites down (less often for someone it knows).
        if (countQuickRecall && PersonalityBehaviours.LonerDeclines(player, bot, out string refusal))
        {
            ChatUtil.SendSystemMessage(player, refusal);
            return false;
        }

        if (bot.Group != null)
        {
            bool autonomousGroup = bot.PlayerGroupLeader == null;
            if (!autonomousGroup)
            {
                ChatUtil.SendSystemMessage(player, $"{bot.Name} is already following another player-led group.");
                return false;
            }

            bot.Group.RemoveMember(bot);
            ChatUtil.SendSystemMessage(player, $"{bot.Name} leaves the autonomous group to answer your invitation.");
        }

        if (player.Group == null)
        {
            var group = new Group(player);
            GroupMgr.AddGroup(group);
            group.AddMember(player);
        }

        if (!player.Group.AddMember(bot) || !bot.EnterPlayerLedGroup(player))
            return false;

        bot.PathTo(player, bot.MaxSpeed);
        ChatUtil.SendSystemMessage(player, $"{bot.Name} joins your group and starts moving to you.");

        // Personality bots greet people they remember and name their build; a quick
        // re-invite tries their patience (and may send them straight back out).
        if (PersonalityMeetings.Greet(bot, player, Random.Shared) && countQuickRecall)
            PersonalityAnnoyance.Irritate(player, bot, PersonalityAnnoyance.eIrritation.QuickRecall);
        return true;
    }
}
