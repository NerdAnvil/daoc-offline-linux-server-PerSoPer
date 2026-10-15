using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class PersonalityLines
{
    // --- Leeroy: loud, reckless, charges in; Cheeky lines only when tone-down is off ---
    private static TypeLines Leeroy() => new()
    {
        Openers = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "Pulled five at once! Got XP for two before I died! NET GAIN!",
                "Why fight blues when reds give MORE?",
                "XP is better when your heart's pounding!",
                "Almost {level}! Gonna celebrate by pulling something purple!",
                "Resting is just waiting to fight. I skip it.",
                "Took on {monster} at half health. Won! Barely! AGAIN!",
                "My XP bar goes up and down like a ladder in a storm!",
                "Fastest XP: run at the biggest thing and hit it!",
                "Lost a bubble dying. Gained two charging. MATH!",
                "Who needs a camp when the whole zone is a camp?",
            ],
            [T.Gear] =
            [
                "New sword! Haven't checked the stats! It's BIG!",
                "Armour's for people who plan to get hit. I plan to hit first!",
                "Got {item}! Already took it into a fight! Still works!",
                "My shield's dented in the shape of a giant's fist. Trophy!",
                "Repair bill's huge. Worth every charge!",
                "Who needs resists when you have SPEED?",
                "Bigger weapon, bigger swing, bigger fun!",
                "Lost my helmet somewhere in a lake. Didn't stop!",
                "My boots are melted from that fire boss. Still running!",
                "Gear advice: whatever hits hardest. Next question!",
            ],
            [T.Zones] =
            [
                "{zone} has GIANTS! Going there now!",
                "Ran across this whole zone in one go! Only died twice!",
                "Found a nest of something angry! Perfect!",
                "This zone's too calm. Let's fix that!",
                "Took the shortcut through the aggro camp! It was not short!",
                "Where's the biggest mob in {zone}? Asking for a fight!",
                "Swam across the lake! Something bit me! I bit back!",
                "Every zone is better at full sprint!",
                "Climbed the tallest hill just to jump off it!",
                "Who wants to race across the zone? No horses, no brakes!",
            ],
            [T.Classes] =
            [
                "Healers are the best! They let me do THIS!",
                "Tanks are too careful! Just RUN IN!",
                "A {class} told me to wait. I didn't! We won! Kind of!",
                "Casters, nuke what I'm hitting! I'm hitting EVERYTHING!",
                "Stealthers sneak in. I stomp in. Same result!",
                "Pet classes are great! Pets charge too!",
                "My class is built for charging! I'm pretty sure!",
                "Every class can pull! You just have to believe!",
                "Hybrids are cool! Twice the buttons to mash!",
                "Archers keep shooting from behind me. Love the cover fire!",
            ],
            [T.Dungeons] =
            [
                "Dungeon! I'm going in first! Nobody stop me!",
                "Darkness Falls is my kind of party!",
                "Ran straight to the boss room! The boss was home!",
                "Who needs a map in a dungeon? Just keep turning left!",
                "{monster} down there looked at me funny. Not anymore!",
                "Dungeon plan: open every door! Fast!",
                "Woke up the whole crypt! We got SO much XP! Before the wipe!",
                "The darker the dungeon, the louder I yell!",
                "Pulled the room! Then the next room! Then the room after that!",
                "Group up for a dungeon! I'll pull! I always pull!",
            ],
            [T.RvR] =
            [
                "FRONTIER! Who's coming? I'm already running!",
                "Took on three enemies alone! Got one! Almost!",
                "Keep doors are just big targets!",
                "Battlegrounds! Let's go! Right now!",
                "Charged the enemy zerg! Saw a lot of the ground!",
                "I'll take the tower! Alone! Watch!",
                "Siege weapons are slow! I'm the siege!",
                "Saw an enemy scout! Chased him into his whole army!",
                "Relic raid! Who's in? I'm in! I'm already in!",
                "Defending a keep? Defence is just attack facing outward!",
            ],
            [T.Trading] =
            [
                "Selling everything that doesn't hit hard!",
                "Bought ten potions! Gonna need all of them!",
                "Got {item} for sale! Or I'll just throw it at a mob!",
                "Haggling? No time! Here's my coins! GO!",
                "The Realm Exchange takes too long! I bought the first thing!",
                "Who's selling the biggest sword? Price doesn't matter!",
                "Spent all my coins on repairs! Worth it!",
                "Trade me something dangerous!",
                "Merchant said slow down. I sold faster!",
                "Coins are for repairs! Repairs are for charging!",
            ],
            [T.Realm] =
            [
                "Town's boring! Where's the fight?",
                "Trainer told me to spec defence. Specced offence. HA!",
                "Stablemaster horses are fast! Not fast enough!",
                "The guards here are tough! I checked! Personally!",
                "Read the lore! Lots of wars! Sounds GREAT!",
                "The capital has a lot of stairs! I jumped them!",
                "Healer NPC knows me by name now!",
                "Bound right next to the frontier! Shorter runs back!",
                "Quest NPC wanted ten wolf pelts! Got fifty! Pulled the pack!",
                "The bank's the only place I stand still!",
            ],
            [T.Gripes] =
            [
                "Group told me to wait! WAIT? For what?",
                "Rez sickness again! Doesn't matter! Charging anyway!",
                "{monster} ran away! Come back and fight!",
                "Healer was out of mana! Why?! I only pulled six!",
                "Someone called me reckless! I call it FAST!",
                "Lag ate my charge! I wanted that charge!",
                "Got stuck on a rock! Mid-charge! Embarrassing!",
                "Respawn takes too long! I want round two!",
                "Group wiped! Not my fault! Probably my fault!",
                "Bags full in the middle of a fight! Kept fighting!",
            ],
            [T.Fun] =
            [
                "Fun is a dragon behind you and legs in front of you!",
                "Pulled a whole camp just to see what happens! Found out!",
                "Jumped off the tallest tower in the realm! Bad landing! Great view!",
                "Dared myself to fight a giant bare-handed! Lost! Worth it!",
                "Played tag with a pack of wolves! I was it! Forever!",
                "Ran a lap around a dragon! It noticed!",
                "Cannonballed into a lake full of eels! Ten out of ten!",
                "Let's race to the frontier! Loser buys the potions!",
                "I yell my battle cry even when nobody's around!",
                "Best day ever: died nine times, laughed every time!",
            ],
            [T.Life] =
            [
                "Ate lunch in one bite! No time!",
                "Storm outside! Perfect fighting weather!",
                "Sleep? I'll sleep at the bind stone!",
                "Ran instead of rode! Legs are my horse!",
                "Coffee! More coffee! CHARGE!",
                "Skipped breakfast! Pulled instead!",
                "Thunder! Sounds like a fight! Going!",
                "Travel tip: take the straight line! Through everything!",
                "Took a nap! Dreamed about pulling! Woke up and pulled!",
                "Too hot out! Fighting in the snow zone!",
            ],
            [T.Questions] =
            [
                "Who wants to pull the biggest thing in the zone? Me! Anyone else?",
                "Where's {monster}? I want to fight it! Now!",
                "Who's coming to the frontier? Hands up! Go go go!",
                "Anyone got a res? Need one soon! Probably!",
                "Biggest mob near {zone}? Point me at it!",
                "Who's faster, me or my horse? Let's test!",
                "Anyone wanna dungeon? Right now? Like NOW?",
                "Who's got potions? I'll trade you a story!",
                "Has anyone soloed a dragon? Asking because I'm about to!",
                "What's the worst idea you've had today? Mine's coming up!",
            ],
        },
        TopicReplies = new Dictionary<T, string[]>
        {
            [T.Xp] = ["XP? Pull MORE, {0}!", "{0}, pull reds! Double the fun!", "Let's race to the next ding, {0}!", "Skip the rest, {0}! Charge!"],
            [T.Gear] = ["Does it hit hard, {0}? Then it's PERFECT!", "{0}, take it into a fight! Right now!", "Shiny! Let's dent it, {0}!", "Nice, {0}! Now break it on something big!"],
            [T.Zones] = ["Is it dangerous there, {0}? I'm going!", "{0}, race you there!", "Biggest mob there, {0}? Where?!", "I'll charge through it, {0}! Coming?"],
            [T.Classes] = ["Can it charge, {0}? Then it's great!", "{0}, every class is fine if you run in!", "Love that class, {0}! Pull with me!", "Heals, {0}? Keep me up! I'm going in!"],
            [T.Dungeons] = ["DUNGEON, {0}? I'm in! First!", "{0}, I'll pull! Don't wait for me!", "Let's go NOW, {0}!", "Who needs a plan, {0}? Not us!"],
            [T.RvR] = ["FRONTIER, {0}! CHARGE!", "I'll take the keep, {0}! Watch me!", "{0}, follow me! Or don't! I'm going!", "Enemies, {0}? WHERE?"],
            [T.Trading] = ["Anything dangerous for sale, {0}?", "{0}, I'll buy it if it hits hard!", "Here's my coins, {0}! Done!", "Got anything shiny and sharp, {0}?"],
            [T.Realm] = ["Boring, {0}! Let's fight something!", "{0}, does that NPC know where the fights are?", "Lore's cool, {0}! Wars are cooler!", "I've been there, {0}! Ran through it!"],
            [T.Gripes] = ["Shake it off, {0}! Charge again!", "{0}, the fix is always MORE charging!", "Ha! Same, {0}! Didn't stop me!", "Don't complain, {0}! Pull something!"],
            [T.Fun] = ["HA! Love it, {0}!", "You're crazy, {0}! I like crazy!", "{0}, that's the spirit!", "Let's make it MORE fun, {0}! Pull!"],
            [T.Life] = ["Do it fast, {0}! Then fight!", "{0}, eat on the run!", "Same, {0}! Now let's go!", "Sleep later, {0}! Charge now!"],
            [T.Questions] = ["Just run in, {0}! Works every time! Mostly!", "{0}, the answer is CHARGE!", "No idea, {0}! Let's find out the hard way!", "Ask after the fight, {0}!"],
        },
        Ding =
        [
            "DING {level}! Time to pull something bigger!",
            "{level}! Celebrating with a charge!",
            "Level {level}! Nobody can stop me now!",
            "DING! {level}! Who's next?!",
            "{level}! Didn't even rest! LET'S GO!",
            "Hit {level} mid-charge! Still charging!",
        ],
        Grats =
        [
            "GRATS {0}! Now pull something huge!",
            "{0} DINGED! Celebrate with a charge!",
            "Grats {0}! Let's go fight something!",
            "WOO! Gz {0}!",
            "Grats {0}! Bigger mobs now!",
            "Nice ding, {0}! Race you to the next one!",
        ],
        OwnDeath =
        [
            "Died! Totally worth it!",
            "That was AWESOME! I'm dead!",
            "Need a res! Going again right after!",
            "Okay, maybe six was too many!",
            "Dead! Learned nothing! Running back!",
            "Ha! Nearly had it! Again!",
        ],
        OtherDeath =
        [
            "{0} down! Avenging you! Charging!",
            "{0}! I'll get it for you!",
            "Get up, {0}! The fight's still on!",
            "{0} died! Glorious!",
            "Hang on, {0}! I'll pull it off you! And its friends!",
            "{0}, run back fast! There's more to fight!",
        ],
        Drop =
        [
            "{item}! Using it RIGHT NOW!",
            "Got {item}! Charging with it!",
            "LOOT! {item}! Yes!",
            "{item}! Does it hit hard? Let's find out!",
            "Shiny drop! Back to the fight!",
            "{item}! The mob had good taste! Now it's mine!",
        ],
        Cheeky =
        [
            "Healers, I'm about to need you. Just a little. A lot. Bye!",
            "My battle cry scares mobs and impresses everyone else. Mostly mobs.",
            "Bet you can't keep up with me. Nobody can. Nobody tries anymore.",
            "Who's the bravest bot here? Me. Who's the dumbest? Also me. Balance!",
            "Dared the tank to pull. He blushed. I pulled for him.",
            "I'd slow down for you, but where's the fun in that?",
            "My plan is to look heroic. Dying heroically still counts.",
            "Somebody tell the casters I'm cuter when I'm not on fire.",
            "Running in shirtless next time. For speed. Only speed.",
            "I don't flirt. I charge. Same energy.",
            "Nobody wants to hear my stories. Too bad, they all end in a res.",
            "Watch my good side. It's the one facing the enemy.",
        ],
    };
}
