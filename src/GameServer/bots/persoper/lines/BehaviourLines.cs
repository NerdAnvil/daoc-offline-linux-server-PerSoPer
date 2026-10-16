namespace DOL.GS;

/// <summary>
/// Lines for the personality types' small behaviours (Step C). All original.
/// {0} = the other one's name; {class} = their class; {spec} = a real build for it.
/// </summary>
public static class BehaviourLines
{
    // --- Leeroy: battle cry when a fight starts (about 1 in 3) ---
    public static readonly string[] LeeroyBattleCries =
    [
        "CHAAAAARGE!",
        "OUTTA MY WAY, IT'S MINE!",
        "NO PLAN! ONLY GLORY!",
        "HERE I COOOOME!",
        "FOR THE REALM! WHICHEVER ONE THIS IS!",
        "SMASH FIRST, ASK NEVER!",
        "TOO LATE TO STOP ME NOW!",
        "LET'S GOOOOO!",
        "HEALS ON ME! PROBABLY! EVENTUALLY!",
        "YOU LOOK LIKE XP!",
        "I GOT THIS! I DON'T GOT THIS! I GOT THIS!",
        "BATTLE TIME, BABY!",
        "WAIT FOR ME? NEVER!",
        "THIS ONE'S FOR THE RESPEC STONE!",
        "INCOMING! IT'S ME! I'M THE INCOMING!",
    ];

    // --- Full Leeroy: he runs in before the group is ready ---
    public static readonly string[] FullLeeroyCries =
    [
        "Okay I've waited long enough. CHAAAARGE!",
        "You guys are still sitting? Cool. I'M GOING IN!",
        "Plan? Here's my plan: GO!",
        "Buffs are for the weak! BANZAAAAI!",
        "I counted to three in my head. GO GO GO!",
        "That {monster} looked at me funny. IT'S ON!",
        "Ready or not, HERE I COME!",
        "Somebody pull! No? FINE, ME!",
    ];

    // --- Loner: turns down a group invite ---
    public static readonly string[] LonerInviteDeclines =
    [
        "Thanks, {0}. I'm better on my own.",
        "No thanks. My pet and I have an understanding.",
        "Groups are just arguments with loot rules. Pass.",
        "Not today, {0}.",
        "I solo. It's not you. Well, it's a bit you.",
        "Kind of you. Still no.",
        "I'll pass. Too many voices in a group.",
        "Maybe another time, {0}. Probably not, though.",
        "I came out here to be alone. That's going well so far.",
        "No. But I'll wave at you from a distance.",
    ];

    // --- Loner: accepts anyway (someone it knows, or a rare good day) ---
    public static readonly string[] LonerInviteAccepts =
    [
        "Fine. Just this once, {0}.",
        "Alright. Don't make it weird.",
        "You again. Okay, I'll come.",
        "Only because it's you, {0}.",
        "Okay. But I'm standing at the back.",
    ];

    // --- Casual: takes a short break while out on its own ---
    public static readonly string[] CasualBreakStarts =
    [
        "brb food",
        "brb, kettle's on.",
        "Short break, my legs fell asleep.",
        "afk a sec, the cat's on the keyboard.",
        "brb, someone's at the door.",
        "Snack break. Important stuff.",
        "Hang on, gotta stretch.",
        "brb, phone.",
        "Two minutes, need a drink.",
        "Pausing for pizza. Priorities.",
    ];

    public static readonly string[] CasualBreakEnds =
    [
        "back",
        "Back! Missed anything?",
        "Okay, fed and ready.",
        "re. Where was I?",
        "Back. That sandwich was worth it.",
        "Alright, back to it. Slowly.",
    ];

    // --- Hardcore: the group has been sitting too long ---
    public static readonly string[] HardcoreSitComplaints =
    [
        "Are we resting or retiring?",
        "Every minute sitting is XP we'll never see.",
        "My power is full. Yours should be too. Let's GO.",
        "We sit any longer and we'll need a housing plot.",
        "Up. Pull. Now.",
        "I didn't log in to watch you meditate.",
        "Somebody pull before I fall asleep.",
        "Downtime is a choice. A bad one.",
        "Tick tock. Mobs respawn without us.",
        "Is this a group or a picnic?",
    ];

    // --- Min/maxer: looks a newcomer over and has opinions ---
    public static readonly string[] MinMaxerInspects =
    [
        "{0}, a {class}? Proper build is {spec}. You're welcome.",
        "Quick tip, {0}: {class} goes {spec}. Anything else is a respec waiting to happen.",
        "Let me guess, {0}, you spread your points around? {class} wants {spec}.",
        "{0}, if you're not running {spec} as a {class}, we need to talk.",
        "Nice {class}, {0}. Is that {spec}? It should be.",
        "Free advice, {0}: {spec}. That's the {class} build. Trust the math.",
        "{0}, I ran the numbers on {class}. {spec}. Done.",
        "Oh, a {class}. {spec} or bust, {0}.",
        "Hope that's {spec}, {0}. Otherwise your {class} is leaving damage on the table.",
        "{0}, every good {class} I know goes {spec}.",
    ];
}
