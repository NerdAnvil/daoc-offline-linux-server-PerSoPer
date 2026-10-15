using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class PersonalityLines
{
    // --- Min/maxer: stats, caps, numbers, "suboptimal" ---
    private static TypeLines MinMaxer() => new()
    {
        Openers = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "XP per kill is a trap. XP per minute including rest is the real number.",
                "Logged my last hour: 62 kills, 9 rests, 1 death. The death cost 14%.",
                "Yellow cons beat oranges once you factor in miss rate. I checked.",
                "Group XP bonus outpaces solo past three members. Below that, solo.",
                "Projected time to {level}: 3 hours 40 if nobody interrupts me.",
                "{monster} has low resists and a short respawn. Statistically ideal.",
                "Camp bonus decays. Rotate camps every 40 minutes for best returns.",
                "Most people rest 30% longer than needed. I keep a timer.",
                "The XP curve steepens after the mid-thirties. Plan your gear around it.",
                "A clean pull is worth more than a fast pull. Variance kills XP.",
            ],
            [T.Gear] =
            [
                "Resists cap at 26% from items. Every point above is wasted.",
                "My template is 2 stat points short of perfect. I can feel it.",
                "Got {item}. Ran the numbers: a side-grade. Keeping it anyway.",
                "Hits cap is the most ignored cap in the game. Check yours.",
                "Skill bonuses beat raw stats for most specs. Look it up.",
                "Never equip armour for looks. Looks have no bonuses.",
                "Overcapped strength again, everyone? I see your charms.",
                "Weapon speed matters less than people think. Damage per second matters.",
                "Spent an hour comparing two rings. They differ by 1%. I chose correctly.",
                "Quality and condition both scale damage. Repair before you complain.",
            ],
            [T.Zones] =
            [
                "{zone} has the best mob density for this level band. I mapped it.",
                "This zone has too many aggro mobs per square. Downtime goes up.",
                "Ideal zone: short respawns, one mob type, no wanderers.",
                "Mixed-level zones are inefficient. Half the spawns con wrong.",
                "Travel time to {zone} eats 6% of a session. Worth it for the density.",
                "I rank zones by kills per hour. This one is third.",
                "Hilly zones slow pulls. Flat ground is underrated.",
                "Spawn points here cluster in threes. Pull from the edge.",
                "This zone's mobs have fire weakness. Spec accordingly.",
                "Too many players in this zone means fewer spawns per person. Basic division.",
            ],
            [T.Classes] =
            [
                "A {class} with the right template outperforms most of its realm.",
                "Hybrids are undervalued until you count their utility per slot.",
                "Pure casters have the highest burst and the worst sustain. Math checks out.",
                "Every class has one correct spec. The rest are opinions.",
                "Healer spec points should go to the main line first. Always.",
                "Tanks need hits cap before anything else.",
                "Pet classes get free damage while resting. That's efficient.",
                "Stealth classes trade group XP for safety. Fine, but measure it.",
                "Respecced twice this week. Third time will be optimal.",
                "Class balance is a spreadsheet nobody else reads. I read it.",
            ],
            [T.Dungeons] =
            [
                "Dungeon loot tables are 80% vendor trash. The other 20% matters.",
                "Darkness Falls drops are worth farming for exactly three items.",
                "Dungeon respawns are faster, but pathing costs you 10% per pull.",
                "Only one drop in this dungeon beats crafted. I know where it spawns.",
                "{monster} has a rare drop rate under 5%. Bring patience and math.",
                "Optimal dungeon group: one tank, one healer, three damage. No exceptions.",
                "Boss kills are low XP per minute. Skip unless the loot is on your list.",
                "A wipe costs a group about 25 minutes. Don't pull extras.",
                "Corners in dungeons are good for line-of-sight pulls. Use them.",
                "I keep a list of dungeon drops by slot. Ask nicely.",
            ],
            [T.RvR] =
            [
                "RvR is a gear check. If you're not capped, you're a donation.",
                "Realm points per hour peak during keep defences, not attacks.",
                "Resist caps matter twice as much in the frontier. Spells hurt.",
                "Realm abilities need planning. Don't spend points on impulse.",
                "Battlegrounds level-cap the fight. Perfect place to test a template.",
                "Siege damage scales poorly. Stand behind it, not in front.",
                "Keep lords have predictable timers. Plan the take.",
                "I count how many hits it takes to drop a caster. Usually four.",
                "Running in alone is negative realm points per hour. Proven.",
                "Relic bonuses are the most important stat nobody talks about.",
            ],
            [T.Trading] =
            [
                "An item is worth exactly its bonuses. Not its colour, not its name.",
                "The Realm Exchange is mispriced on half its listings. Free profit.",
                "I'll buy {item} if the bonuses match my template. Show me.",
                "Selling at the merchant is the worst rate. Use the exchange.",
                "Potions are fine. Buffs are cheaper over a full session.",
                "Crafted gear at full quality beats most drops. Fact.",
                "I value coins by repairs per hour. Current rate: acceptable.",
                "Selling spare charms. Each one has a stat table. Ask.",
                "Bought a cheap side-grade to free up a jewel slot. Net gain.",
                "If you haggle, bring numbers. I will.",
            ],
            [T.Realm] =
            [
                "The trainers here explain specs badly. I could do better.",
                "Stable routes save an average of four minutes each. I timed them.",
                "Bind point placement is an optimisation problem. Most people fail it.",
                "The lore books in town are interesting. Low stat value, though.",
                "Town merchants sell the same items at slightly different prices. Shop smart.",
                "Healer NPCs fix rez sickness faster than waiting. Worth the coins.",
                "Our capital layout adds two minutes to every bank visit.",
                "Guards here have higher level than needed. Inefficient design.",
                "The realm's best crafter NPC is underused. Fewer queues for me.",
                "Every NPC title is a hint about loot. I read all of them.",
            ],
            [T.Gripes] =
            [
                "Someone in town is wearing four strength items. Overcapped by miles.",
                "Missed three hits in a row on a blue con. Statistically rude.",
                "My resists are 1% below cap and I can't find a fix.",
                "Group leader pulled two at once. Unoptimal and loud.",
                "People ask me for advice and then ignore it.",
                "The trainer reset my plan when I misclicked. Never again.",
                "{monster} resisted three spells in a row. Odds were 4%.",
                "Nobody tracks their downtime. It hurts to watch.",
                "Lost an item to a bad roll. The odds were in my favour.",
                "A buff fell off mid-fight. The timer should be visible.",
            ],
            [T.Fun] =
            [
                "Built a spreadsheet for my spreadsheet. Very relaxing.",
                "Fun fact: I can name every cap in the game from memory.",
                "Calculated the odds of a joke being funny. Low.",
                "Optimised my emotes. Fewer keystrokes, same wave.",
                "Did a run with zero wasted buffs. Best night in weeks.",
                "My idea of a party: everyone capped, nobody overcapped.",
                "Timed how long people stand in town doing nothing. Average: six minutes.",
                "Played an entire session with perfect resists. Nobody noticed. I noticed.",
                "Ranked every horse in the stables by speed. They're identical. Still fun.",
                "Humour is not a stat. If it were, I'd have capped it.",
            ],
            [T.Life] =
            [
                "Meal timing matters. Eat during the respawn, never during a pull.",
                "Seven and a half hours of sleep. Five cycles. Optimal.",
                "Rainy days are good for template planning.",
                "Travel time is dead time. I plan specs while I ride.",
                "Coffee gives a measurable boost for about two hours.",
                "Tracked my snacks last week. Too many. Adjusting.",
                "Weather here never changes stats. Disappointing.",
                "Shortest walk to the kitchen: 14 steps. Measured.",
                "Breaks every 90 minutes keep focus at its peak.",
                "Even my holidays have a spreadsheet.",
            ],
            [T.Questions] =
            [
                "What are everyone's resists right now? Exact numbers, please.",
                "Anyone have the drop rate for {item}? Real data, not guesses.",
                "Who's at hits cap? Raise your hand. Nobody? As expected.",
                "Which spec are people running on a {class}? I want to compare.",
                "Has anyone timed the respawn on {monster}?",
                "Is anyone selling a ring with skill bonuses?",
                "What's the fastest route to {zone}? I have a guess, I want a second source.",
                "Who here actually reads their combat log?",
                "Anyone running a template I can look at? Purely for science.",
                "How many points did everyone put in their main line?",
            ],
        },
        TopicReplies = new Dictionary<T, string[]>
        {
            [T.Xp] = ["Per minute or per kill, {0}? It matters.", "{0}, include rest time and redo that number.", "Decent rate, {0}. Not optimal.", "Track it for a week, {0}, then we'll talk."],
            [T.Gear] = ["Bonuses, {0}? Show me the numbers.", "{0}, that's a side-grade at best.", "Are you capped with that, {0}?", "Nice, {0}. Now check you're not overcapped."],
            [T.Zones] = ["Mob density, {0}? That's the metric.", "{0}, that zone has bad respawn timers.", "I ranked that zone, {0}. Mid-table.", "Good pick, {0}. Statistically."],
            [T.Classes] = ["Which spec, {0}? That changes everything.", "{0}, that class is fine with the right template.", "Hits cap first, {0}.", "Interesting choice, {0}. Suboptimal, but interesting."],
            [T.Dungeons] = ["Only worth it for one drop, {0}.", "{0}, bring a real healer or don't go.", "I'll come, {0}, if we follow my pull order.", "Drop rates there are poor, {0}. I checked."],
            [T.RvR] = ["Are your resists capped, {0}?", "{0}, defend, don't attack. Better points per hour.", "After a respec, {0}.", "Bring numbers, {0}. The frontier does."],
            [T.Trading] = ["Worth its bonuses, {0}. Nothing more.", "{0}, check the exchange first. Better rate.", "Show me the stat table, {0}.", "Fair price, {0}, if it's at full quality."],
            [T.Realm] = ["{0}, that NPC's stock is overpriced.", "I timed that stable route, {0}. Four minutes.", "Bind closer, {0}. Basic optimisation.", "Interesting lore, {0}. No stats, though."],
            [T.Gripes] = ["Statistically that happens, {0}. Annoying, though.", "{0}, check your resists. It's usually resists.", "Variance, {0}. It evens out.", "I logged the same problem, {0}."],
            [T.Fun] = ["Mildly amusing, {0}. I'd rate it 6 out of 10.", "Ha. {0}, that was efficient humour.", "{0}, was that a joke? Measuring.", "Funny, {0}. Not a stat, but funny."],
            [T.Life] = ["Have you tracked that, {0}?", "{0}, sleep cycles. Look them up.", "Optimal, {0}.", "Interesting, {0}. I'd schedule it better."],
            [T.Questions] = ["Depends on your template, {0}.", "{0}, short answer: cap it first.", "I have a spreadsheet for that, {0}.", "Source needed, {0}, but probably yes."],
        },
        Ding =
        [
            "Ding {level}. Exactly as projected.",
            "{level}. Three minutes ahead of my estimate.",
            "Level {level}. Recalculating caps now.",
            "Ding {level}. New stat caps, new spreadsheet.",
            "{level} reached. Spec points allocated in advance.",
            "Hit {level}. Efficiency this level: 94%.",
        ],
        Grats =
        [
            "Grats, {0}. Recheck your caps.",
            "Nice, {0}. Spend those points wisely.",
            "Gz {0}. What was your XP per hour?",
            "Grats, {0}. Your caps just went up.",
            "Well done, {0}. Statistically on time.",
            "Grats {0}. Don't misclick at the trainer.",
        ],
        OwnDeath =
        [
            "Dead. That pull had a 12% risk. The 12% happened.",
            "Died. Resists were the problem. They're always the problem.",
            "Fatal miscalculation. Logging it.",
            "Dead. Variance, not skill.",
            "Death recorded. Adjusting my pull model.",
            "Died to an add. Pathing data was wrong.",
        ],
        OtherDeath =
        [
            "{0} down. Check your resists.",
            "{0}, that was avoidable. I have numbers.",
            "Rough, {0}. What were your hits?",
            "{0} died. I'll add it to the log.",
            "Suboptimal, {0}. Rez and recalculate.",
            "{0}, you overpulled. Classic.",
        ],
        Drop =
        [
            "{item}. Checking bonuses.",
            "Got {item}. Upgrade by 3%. Acceptable.",
            "{item} dropped. Side-grade, but a good one.",
            "{item}. That fills a gap in my template.",
            "Rare drop. The odds were under 5%.",
            "Oh, {item}. Running the numbers now.",
        ],
        Cheeky = null,
    };
}
