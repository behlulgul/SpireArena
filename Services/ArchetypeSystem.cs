using System.Collections.Generic;

namespace SpireArena;

/// <summary>
/// Defines deck archetypes per character and provides archetype-aware
/// rating bonuses. When the player selects a build strategy (e.g. "Shiv"),
/// cards matching that archetype's synergy tags and key cards receive
/// a contextual rating boost.
/// </summary>
public static class ArchetypeSystem
{
    public class Archetype
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Character { get; init; } = "";
        public string Description { get; init; } = "";
        /// <summary>Tags that gain a bonus when this archetype is active.</summary>
        public HashSet<string> SynergyTags { get; init; } = [];
        /// <summary>Card IDs that are core to this archetype — extra bonus.</summary>
        public HashSet<string> KeyCardIds { get; init; } = [];
        /// <summary>
        /// Build-specific card ratings. When this archetype is active, these
        /// ratings replace the global BaseRating for the given card IDs.
        /// Cards not listed here fall back to the global BaseRating + synergy.
        /// </summary>
        public Dictionary<string, int> CardRatingOverrides { get; init; } = [];
    }

    /// <summary>Currently active archetype, or null for no archetype filter.</summary>
    public static Archetype? ActiveArchetype { get; private set; }

    /// <summary>Current character filter for cycling. Empty = cycle all.</summary>
    private static string _currentCharacter = "";

    /// <summary>All unique character names across registered archetypes.</summary>
    public static List<string> AllCharacters
    {
        get
        {
            var chars = new List<string>();
            foreach (var a in _allArchetypes)
            {
                if (!chars.Contains(a.Character))
                    chars.Add(a.Character);
            }
            return chars;
        }
    }

    /// <summary>Cards picked during the run (card ID → display name).</summary>
    private static readonly List<PickedCard> _pickedCards = [];
    public static IReadOnlyList<PickedCard> PickedCards => _pickedCards;

    public class PickedCard
    {
        public string CardId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int Rating { get; init; }
        public bool IsSynergy { get; init; }
    }

    // ─── Rating bonuses ───
    private const int TagMatchBonus = 2;
    private const int KeyCardBonus = 3;
    private const int MaxArchetypeBonus = 4;

    // ─── All archetypes ───
    private static readonly List<Archetype> _allArchetypes = [];

    public static IReadOnlyList<Archetype> AllArchetypes => _allArchetypes;

    static ArchetypeSystem()
    {
        RegisterArchetypes();
    }

    private static void RegisterArchetypes()
    {
        // ══════════════════════════════════════
        //  SILENT
        // ══════════════════════════════════════
        _allArchetypes.Add(new Archetype
        {
            Id = "silent_shiv",
            Name = "Shiv",
            Character = "Silent",
            Description = "Generate, buff, and spam Shivs for massive multi-hit damage.",
            SynergyTags = ["shiv", "shiv-scaling", "multi-hit"],
            KeyCardIds =
            [
                "blade_dance", "accuracy", "infinite_blade", "hidden_dagger",
                "knife_trap", "finisher", "afterimage", "cloak_and_dagger",
                "leading_strike", "fan_of_knives", "serpent_form",
                "up_my_sleeve", "accelerant"
            ],
            CardRatingOverrides = new()
            {
                // ── Core shiv generators & scalers → S/A tier in this build ──
                ["blade_dance"] = 10,
                ["accuracy"] = 10,
                ["afterimage"] = 10,
                ["infinite_blade"] = 9,
                ["cloak_and_dagger"] = 9,
                ["knife_trap"] = 9,
                ["finisher"] = 9,
                ["fan_of_knives"] = 8,
                ["up_my_sleeve"] = 8,
                ["hidden_dagger"] = 8,
                ["leading_strike"] = 8,
                ["accelerant"] = 8,
                ["serpent_form"] = 7,
                // ── Good generic support ──
                ["footwork"] = 9,
                ["piercing_wail"] = 9,
                ["well_laid_plans"] = 8,
                ["acrobatics"] = 8,
                ["adrenaline"] = 10,
                ["backflip"] = 7,
                ["prepared"] = 7,
                ["escape_plan"] = 7,
                ["burst"] = 7,
                // ── Weak in shiv build ──
                ["poisoned_stab"] = 3,
                ["deadly_poison"] = 3,
                ["noxious_fumes"] = 4,
                ["outbreak"] = 3,
                ["bouncing_flask"] = 2,
                ["envenom"] = 2,
                ["snakebite"] = 2,
                ["corrosive_water"] = 4,
                ["bubble_bubble"] = 2,
                // ── Discard cards mediocre here ──
                ["calculated_gamble"] = 5,
                ["tactician"] = 5,
                ["reflex"] = 5,
                ["tools_of_the_trade"] = 6,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "silent_poison",
            Name = "Poison",
            Character = "Silent",
            Description = "Stack Poison for exponential ramping damage over time.",
            SynergyTags = ["poison", "poison-synergy"],
            KeyCardIds =
            [
                "poisoned_stab", "deadly_poison", "noxious_fumes", "outbreak",
                "accelerant", "bubble_bubble", "bouncing_flask", "envenom",
                "corrosive_water", "burst", "snakebite"
            ],
            CardRatingOverrides = new()
            {
                // ── Core poison cards → top tier ──
                ["noxious_fumes"] = 10,
                ["corrosive_water"] = 10,
                ["envenom"] = 9,
                ["deadly_poison"] = 9,
                ["bouncing_flask"] = 9,
                ["outbreak"] = 9,
                ["snakebite"] = 8,
                ["poisoned_stab"] = 8,
                ["bubble_bubble"] = 8,
                ["accelerant"] = 8,
                ["burst"] = 9,
                // ── Good generic support ──
                ["footwork"] = 9,
                ["piercing_wail"] = 9,
                ["well_laid_plans"] = 8,
                ["adrenaline"] = 10,
                ["acrobatics"] = 8,
                ["backflip"] = 7,
                ["escape_plan"] = 7,
                ["malaise"] = 8,
                // ── Shiv cards weak in poison ──
                ["blade_dance"] = 4,
                ["accuracy"] = 2,
                ["infinite_blade"] = 3,
                ["cloak_and_dagger"] = 4,
                ["knife_trap"] = 3,
                ["finisher"] = 3,
                ["fan_of_knives"] = 4,
                ["up_my_sleeve"] = 3,
                ["hidden_dagger"] = 3,
                // ── Discard cards mediocre ──
                ["calculated_gamble"] = 5,
                ["tactician"] = 5,
                ["reflex"] = 5,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "silent_sly",
            Name = "Sly / Discard",
            Character = "Silent",
            Description = "Cycle cards fast and play Sly cards for free via discard.",
            SynergyTags = ["discard", "discard-synergy", "sly", "cycle", "draw"],
            KeyCardIds =
            [
                "acrobatics", "prepared", "calculated_gamble", "reflex",
                "tactician", "tools_of_the_trade", "flick_flack", "ricochet",
                "untouchable", "dagger_throw", "haze", "speedster",
                "well_laid_plans", "hand_trick", "abrasive", "master_plan"
            ],
            CardRatingOverrides = new()
            {
                // ── Core discard/sly engine → top tier ──
                ["tools_of_the_trade"] = 10,
                ["acrobatics"] = 10,
                ["calculated_gamble"] = 10,
                ["reflex"] = 10,
                ["tactician"] = 10,
                ["prepared"] = 9,
                ["well_laid_plans"] = 9,
                ["untouchable"] = 10,
                ["flick_flack"] = 9,
                ["ricochet"] = 9,
                ["dagger_throw"] = 8,
                ["haze"] = 8,
                ["speedster"] = 8,
                ["hand_trick"] = 8,
                ["abrasive"] = 8,
                ["master_plan"] = 7,
                ["adrenaline"] = 10,
                // ── Good generic ──
                ["piercing_wail"] = 9,
                ["footwork"] = 8,
                ["escape_plan"] = 7,
                ["backflip"] = 7,
                ["burst"] = 7,
                // ── Poison/shiv weak here ──
                ["poisoned_stab"] = 3,
                ["deadly_poison"] = 3,
                ["noxious_fumes"] = 4,
                ["bouncing_flask"] = 2,
                ["envenom"] = 2,
                ["snakebite"] = 2,
                ["blade_dance"] = 4,
                ["accuracy"] = 2,
                ["infinite_blade"] = 3,
                ["knife_trap"] = 3,
                ["finisher"] = 3,
            }
        });

        // ══════════════════════════════════════
        //  IRONCLAD (source: Mobalytics Ironclad Guide)
        // ══════════════════════════════════════
        _allArchetypes.Add(new Archetype
        {
            Id = "ironclad_strength",
            Name = "Strength",
            Character = "Ironclad",
            Description = "Scale Strength and play multi-hit attacks for devastating damage.",
            SynergyTags = ["strength", "strength-synergy", "multi-hit"],
            KeyCardIds =
            [
                "demon_form", "inflame", "rupture", "twin_strike",
                "fight_me", "whirlwind", "brand", "thrash",
                "primal_force", "aggression"
            ],
            CardRatingOverrides = new()
            {
                // ── Core strength scalers ──
                ["demon_form"] = 10,
                ["inflame"] = 9,
                ["primal_force"] = 10,
                ["rupture"] = 9,
                ["aggression"] = 9,
                ["brand"] = 9,
                ["thrash"] = 9,
                // ── Multi-hit payoffs ──
                ["twin_strike"] = 8,
                ["whirlwind"] = 8,
                ["sword_boomerang"] = 7,
                ["fight_me"] = 7,
                // ── Good generic ──
                ["offering"] = 10,
                ["headbutt"] = 9,
                ["battle_trance"] = 9,
                ["feed"] = 9,
                ["expect_a_fight"] = 9,
                ["burning_pact"] = 7,
                ["pommel_strike"] = 7,
                ["shrug_it_off"] = 7,
                ["flame_barrier"] = 7,
                // ── Exhaust/block stuff weak here ──
                ["corruption"] = 4,
                ["dark_embrace"] = 4,
                ["feel_no_pain"] = 4,
                ["body_slam"] = 3,
                ["barricade"] = 3,
                ["true_grit"] = 5,
                ["juggernaut"] = 3,
                ["stone_armor"] = 2,
                ["impervious"] = 5,
                ["iron_wave"] = 3,
                // ── Bloodletting niche ──
                ["bloodletting"] = 6,
                ["hemokinesis"] = 6,
                ["inferno"] = 5,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "ironclad_block",
            Name = "Block / Barricade",
            Character = "Ironclad",
            Description = "Retain and stack Block, then slam enemies with Body Slam.",
            SynergyTags = ["block", "block-synergy", "retain-block"],
            KeyCardIds =
            [
                "body_slam", "barricade", "shrug_it_off", "true_grit",
                "flame_barrier", "taunt", "stone_armor", "juggernaut",
                "crimson_mantle", "impervious", "unmovable", "colossus"
            ],
            CardRatingOverrides = new()
            {
                // ── Core block engine ──
                ["barricade"] = 10,
                ["body_slam"] = 10,
                ["impervious"] = 10,
                ["unmovable"] = 10,
                ["colossus"] = 9,
                ["shrug_it_off"] = 9,
                ["flame_barrier"] = 9,
                ["true_grit"] = 9,
                ["juggernaut"] = 9,
                ["taunt"] = 8,
                ["crimson_mantle"] = 8,
                ["stone_armor"] = 7,
                ["iron_wave"] = 6,
                // ── Good generic ──
                ["offering"] = 9,
                ["headbutt"] = 8,
                ["battle_trance"] = 8,
                ["feed"] = 8,
                ["expect_a_fight"] = 8,
                ["feel_no_pain"] = 7,
                ["burning_pact"] = 6,
                // ── Strength scaling weak here ──
                ["demon_form"] = 4,
                ["inflame"] = 4,
                ["rupture"] = 3,
                ["twin_strike"] = 3,
                ["whirlwind"] = 4,
                ["aggression"] = 4,
                ["primal_force"] = 5,
                ["sword_boomerang"] = 3,
                // ── Bloodletting irrelevant ──
                ["bloodletting"] = 4,
                ["inferno"] = 3,
                ["hemokinesis"] = 4,
                ["breakthrough"] = 4,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "ironclad_exhaust",
            Name = "Exhaust",
            Character = "Ironclad",
            Description = "Exhaust cards to fuel Corruption + Feel No Pain + Dark Embrace.",
            SynergyTags = ["exhaust", "exhaust-synergy"],
            KeyCardIds =
            [
                "corruption", "dark_embrace", "feel_no_pain", "true_grit",
                "body_slam", "ashen_strike", "burning_pact", "evil_eye",
                "forgotten_ritual", "brand", "offering", "pacts_end",
                "thrash", "juggernaut", "second_wind", "fiend_fire"
            ],
            CardRatingOverrides = new()
            {
                // ── Core exhaust engine ──
                ["corruption"] = 10,
                ["dark_embrace"] = 10,
                ["feel_no_pain"] = 10,
                ["second_wind"] = 9,
                ["fiend_fire"] = 9,
                ["burning_pact"] = 9,
                ["true_grit"] = 9,
                ["offering"] = 10,
                ["evil_eye"] = 9,
                ["pacts_end"] = 8,
                ["forgotten_ritual"] = 8,
                ["ashen_strike"] = 7,
                // ── Good with exhaust ──
                ["body_slam"] = 7,
                ["juggernaut"] = 7,
                ["brand"] = 8,
                ["thrash"] = 7,
                ["headbutt"] = 8,
                ["battle_trance"] = 8,
                ["feed"] = 8,
                ["expect_a_fight"] = 8,
                ["flame_barrier"] = 7,
                ["shrug_it_off"] = 7,
                // ── Strength scaling secondary ──
                ["demon_form"] = 5,
                ["inflame"] = 5,
                ["rupture"] = 4,
                ["primal_force"] = 6,
                // ── Barricade/block stacking weak ──
                ["barricade"] = 3,
                ["impervious"] = 5,
                ["stone_armor"] = 3,
                ["colossus"] = 4,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "ironclad_bloodletting",
            Name = "Bloodletting",
            Character = "Ironclad",
            Description = "Self-damage to gain Strength via Rupture and fuel Inferno.",
            SynergyTags = ["bloodletting", "hp-loss", "strength-synergy"],
            KeyCardIds =
            [
                "rupture", "inferno", "breakthrough", "bloodletting",
                "hemokinesis", "crimson_mantle", "brand", "offering",
                "feed", "tear_asunder"
            ],
            CardRatingOverrides = new()
            {
                // ── Core bloodletting / self-damage ──
                ["rupture"] = 10,
                ["bloodletting"] = 10,
                ["inferno"] = 10,
                ["hemokinesis"] = 9,
                ["breakthrough"] = 9,
                ["offering"] = 10,
                ["feed"] = 10,
                ["tear_asunder"] = 8,
                ["crimson_mantle"] = 8,
                ["brand"] = 8,
                // ── Strength payoffs (from Rupture) ──
                ["twin_strike"] = 7,
                ["whirlwind"] = 7,
                ["thrash"] = 7,
                ["aggression"] = 7,
                ["primal_force"] = 7,
                // ── Good generic ──
                ["headbutt"] = 8,
                ["battle_trance"] = 8,
                ["expect_a_fight"] = 8,
                ["burning_pact"] = 7,
                ["flame_barrier"] = 6,
                ["shrug_it_off"] = 6,
                // ── Block stacking irrelevant ──
                ["barricade"] = 3,
                ["body_slam"] = 3,
                ["impervious"] = 4,
                ["juggernaut"] = 3,
                ["stone_armor"] = 2,
                ["colossus"] = 3,
                // ── Pure exhaust less relevant ──
                ["corruption"] = 5,
                ["dark_embrace"] = 5,
                ["feel_no_pain"] = 5,
            }
        });

        // ══════════════════════════════════════
        //  REGENT
        // ══════════════════════════════════════
        _allArchetypes.Add(new Archetype
        {
            Id = "regent_cosmic",
            Name = "Cosmic / Scaling",
            Character = "Regent",
            Description = "Scale cosmic powers and forms for late-game dominance.",
            SynergyTags = ["form", "scaling", "summon"],
            KeyCardIds =
            [
                "regent_void_form", "regent_big_bang", "regent_genesis",
                "regent_child_of_the_stars", "regent_convergence",
                "regent_particle_wall", "regent_seven_stars"
            ],
            CardRatingOverrides = new()
            {
                // ── Core cosmic scaling ──
                ["regent_void_form"] = 10,
                ["regent_big_bang"] = 10,
                ["regent_genesis"] = 10,
                ["regent_child_of_the_stars"] = 10,
                ["regent_convergence"] = 10,
                ["regent_particle_wall"] = 10,
                ["regent_seven_stars"] = 9,
                ["regent_foregone_conclusion"] = 10,
                ["regent_guards"] = 9,
                ["regent_reflect"] = 9,
                ["regent_glow"] = 9,
                // ── Good scaling support ──
                ["regent_neutron_aegis"] = 8,
                ["regent_bombardment"] = 8,
                ["regent_dying_star"] = 8,
                ["regent_gamma_blast"] = 8,
                ["regent_shining_strike"] = 7,
                ["regent_comet"] = 7,
                ["regent_royalties"] = 7,
                ["regent_gather_light"] = 7,
                ["regent_summon_force"] = 7,
                ["regent_astral_pulse"] = 7,
                ["regent_cloak_of_stars"] = 7,
                // ── Weaker in scaling build ──
                ["regent_kingly_punch"] = 3,
                ["regent_kingly_kick"] = 3,
                ["regent_heirloom_hammer"] = 3,
                ["regent_crash_landing"] = 2,
                ["regent_monologue"] = 3,
                ["regent_know_thy_place"] = 3,
            }
        });

        // ══════════════════════════════════════
        //  DEFECT (source: Mobalytics Defect Guide)
        // ══════════════════════════════════════
        _allArchetypes.Add(new Archetype
        {
            Id = "defect_claw",
            Name = "Claw",
            Character = "Defect",
            Description = "Spam 0-cost attacks, cycle cards, and scale Claw damage.",
            SynergyTags = ["claw", "zero-cost", "cycle", "claw-support"],
            KeyCardIds =
            [
                "claw", "scrape", "all_for_one", "feral",
                "momentum_strike", "beam_cell", "go_for_the_eyes",
                "flash_of_steel", "ftl", "hologram", "skim",
                "machine_learning", "panache", "secret_weapon"
            ],
            CardRatingOverrides = new()
            {
                // ── Core claw engine ──
                ["claw"] = 10,
                ["all_for_one"] = 10,
                ["scrape"] = 10,
                ["hologram"] = 10,
                ["skim"] = 10,
                ["feral"] = 9,
                ["ftl"] = 9,
                ["machine_learning"] = 9,
                ["flash_of_steel"] = 8,
                ["momentum_strike"] = 8,
                ["beam_cell"] = 8,
                ["go_for_the_eyes"] = 8,
                ["panache"] = 8,
                ["secret_weapon"] = 8,
                // ── Good generic ──
                ["echo_form"] = 10,
                ["double_energy"] = 9,
                ["genetic_algo"] = 8,
                ["turbo"] = 8,
                ["reboot"] = 7,
                ["coolant"] = 7,
                // ── Orb/Focus stuff weak in claw ──
                ["defragment"] = 4,
                ["glacier"] = 5,
                ["ball_lightning"] = 5,
                ["cold_snap"] = 4,
                ["coolheaded"] = 4,
                ["capacitor"] = 3,
                ["loop"] = 2,
                ["thunder"] = 4,
                ["hailstorm"] = 4,
                ["multi_cast"] = 3,
                ["barrage"] = 4,
                ["compile_driver"] = 5,
                ["tesla_coil"] = 4,
                ["lightning_rod"] = 3,
                ["storm"] = 2,
                ["chaos"] = 4,
                ["voltaic"] = 4,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "defect_orb",
            Name = "Orb / Focus",
            Character = "Defect",
            Description = "Channel and Evoke orbs with Focus for passive damage and Block.",
            SynergyTags = ["orb-synergy", "focus", "frost", "lightning", "focus-synergy"],
            KeyCardIds =
            [
                "defragment", "glacier", "ball_lightning", "cold_snap",
                "coolheaded", "barrage", "compile_driver", "lightning_rod",
                "chaos", "capacitor", "loop", "thunder", "hailstorm",
                "multi_cast", "voltaic", "tesla_coil", "modded"
            ],
            CardRatingOverrides = new()
            {
                // ── Core orb/focus engine ──
                ["defragment"] = 10,
                ["glacier"] = 10,
                ["echo_form"] = 10,
                ["capacitor"] = 9,
                ["loop"] = 9,
                ["coolheaded"] = 9,
                ["cold_snap"] = 9,
                ["ball_lightning"] = 9,
                ["hailstorm"] = 9,
                ["multi_cast"] = 9,
                ["thunder"] = 9,
                ["voltaic"] = 9,
                ["barrage"] = 8,
                ["compile_driver"] = 8,
                ["tesla_coil"] = 8,
                ["lightning_rod"] = 7,
                ["chaos"] = 7,
                ["modded"] = 9,
                // ── Good generic ──
                ["double_energy"] = 9,
                ["genetic_algo"] = 9,
                ["fusion"] = 10,
                ["spinner"] = 9,
                ["hologram"] = 8,
                ["skim"] = 8,
                ["buffer"] = 8,
                ["reboot"] = 7,
                ["coolant"] = 8,
                // ── Claw stuff weak here ──
                ["claw"] = 2,
                ["scrape"] = 4,
                ["all_for_one"] = 4,
                ["feral"] = 4,
                ["momentum_strike"] = 4,
                ["beam_cell"] = 5,
                ["go_for_the_eyes"] = 5,
                ["flash_of_steel"] = 4,
                ["ftl"] = 5,
                ["panache"] = 4,
                // ── Other ──
                ["hyperbeam"] = 3,
                ["storm"] = 6,
                ["creative_ai"] = 6,
                ["meteor_strike"] = 5,
            }
        });

        // ══════════════════════════════════════
        //  NECROBINDER (source: Mobalytics Necrobinder Guide)
        // ══════════════════════════════════════
        _allArchetypes.Add(new Archetype
        {
            Id = "necro_doom",
            Name = "Doom",
            Character = "Necrobinder",
            Description = "Stack Doom to execute enemies when HP drops below threshold.",
            SynergyTags = ["doom", "doom-synergy", "doom-support"],
            KeyCardIds =
            [
                "blight_strike", "defile", "negative_pulse", "scourge",
                "deathbringer", "delay", "deaths_door", "end_of_days",
                "no_escape", "oblivion", "shroud", "times_up"
            ],
            CardRatingOverrides = new()
            {
                // ── Core doom engine ──
                ["deathbringer"] = 10,
                ["end_of_days"] = 10,
                ["deaths_door"] = 10,
                ["no_escape"] = 10,
                ["shroud"] = 9,
                ["defile"] = 9,
                ["blight_strike"] = 9,
                ["negative_pulse"] = 9,
                ["scourge"] = 8,
                ["oblivion"] = 8,
                ["times_up"] = 8,
                ["delay"] = 8,
                // ── Good generic Necrobinder ──
                ["neurosurge"] = 9,
                ["borrowed_time"] = 9,
                ["graveblast"] = 8,
                ["dredge"] = 8,
                ["capture_spirit"] = 8,
                ["seance"] = 8,
                ["undeath"] = 8,
                ["cleanse"] = 8,
                ["demesne"] = 7,
                ["friendship"] = 7,
                ["putrefy"] = 8,
                ["lethality"] = 8,
                ["grave_warden"] = 7,
                ["haunt"] = 6,
                // ── Osty stuff weak in doom ──
                ["pull_aggro"] = 3,
                ["snap"] = 4,
                ["high_five"] = 4,
                ["rattle"] = 4,
                ["fetch"] = 4,
                ["flatten"] = 4,
                ["necro_mastery"] = 5,
                ["reanimate"] = 4,
                ["sic_em"] = 4,
                ["spur"] = 3,
                ["bone_shards"] = 4,
                // ── Bottom tier ──
                ["bury"] = 3,
                ["danse_macabre"] = 3,
                ["reaper_form"] = 3,
            }
        });

        _allArchetypes.Add(new Archetype
        {
            Id = "necro_osty",
            Name = "Osty",
            Character = "Necrobinder",
            Description = "Grow and protect Osty with Summon, then attack with Osty cards.",
            SynergyTags = ["osty", "summon", "summon-synergy"],
            KeyCardIds =
            [
                "pull_aggro", "snap", "high_five", "rattle",
                "fetch", "flatten", "necro_mastery", "reanimate",
                "sic_em", "spur", "bone_shards"
            ],
            CardRatingOverrides = new()
            {
                // ── Core osty engine ──
                ["sic_em"] = 10,
                ["necro_mastery"] = 10,
                ["flatten"] = 10,
                ["reanimate"] = 10,
                ["high_five"] = 9,
                ["rattle"] = 9,
                ["fetch"] = 9,
                ["pull_aggro"] = 9,
                ["snap"] = 8,
                ["spur"] = 8,
                ["bone_shards"] = 8,
                // ── Good generic Necrobinder ──
                ["neurosurge"] = 9,
                ["friendship"] = 10,
                ["borrowed_time"] = 8,
                ["graveblast"] = 7,
                ["dredge"] = 8,
                ["capture_spirit"] = 8,
                ["seance"] = 8,
                ["undeath"] = 8,
                ["cleanse"] = 8,
                ["demesne"] = 9,
                ["putrefy"] = 7,
                ["lethality"] = 7,
                ["grave_warden"] = 7,
                // ── Doom stuff weak in osty ──
                ["blight_strike"] = 4,
                ["defile"] = 4,
                ["negative_pulse"] = 4,
                ["scourge"] = 4,
                ["deathbringer"] = 4,
                ["delay"] = 4,
                ["deaths_door"] = 4,
                ["end_of_days"] = 4,
                ["no_escape"] = 4,
                ["oblivion"] = 3,
                ["shroud"] = 4,
                ["times_up"] = 3,
                // ── Bottom tier ──
                ["bury"] = 3,
                ["danse_macabre"] = 3,
                ["reaper_form"] = 3,
                ["haunt"] = 4,
            }
        });
    }

    /// <summary>
    /// Get archetypes available for a given character name.
    /// </summary>
    public static List<Archetype> GetArchetypesForCharacter(string character)
    {
        var result = new List<Archetype>();
        foreach (var a in _allArchetypes)
        {
            if (a.Character.Equals(character, System.StringComparison.OrdinalIgnoreCase))
                result.Add(a);
        }
        return result;
    }

    /// <summary>
    /// Set the active archetype. Pass null to disable.
    /// </summary>
    public static void SetActiveArchetype(Archetype? archetype)
    {
        ActiveArchetype = archetype;
        if (archetype != null)
            MainFile.Logger.Info($"[Archetype] Active build: {archetype.Name} ({archetype.Character})");
        else
            MainFile.Logger.Info("[Archetype] Build selection cleared.");
    }

    /// <summary>
    /// Cycle to the next character class. Resets the active archetype
    /// to the first build of the new character.
    /// </summary>
    public static void CycleCharacter()
    {
        var chars = AllCharacters;
        if (chars.Count == 0) return;

        int idx = string.IsNullOrEmpty(_currentCharacter)
            ? -1
            : chars.IndexOf(_currentCharacter);

        int next = idx + 1;
        if (next >= chars.Count)
        {
            // Wrap back to "no character" → null archetype
            _currentCharacter = "";
            SetActiveArchetype(null);
            return;
        }

        _currentCharacter = chars[next];
        var archsForChar = GetArchetypesForCharacter(_currentCharacter);
        SetActiveArchetype(archsForChar.Count > 0 ? archsForChar[0] : null);
    }

    /// <summary>
    /// Cycle to the next archetype within the current character.
    /// If no character is selected, cycles character first.
    /// Cycles: build1 → build2 → ... → build1 (stays within same character).
    /// </summary>
    public static void CycleArchetype()
    {
        if (_allArchetypes.Count == 0) return;

        // If no character selected yet, select the first character + first build
        if (string.IsNullOrEmpty(_currentCharacter))
        {
            CycleCharacter();
            return;
        }

        var archsForChar = GetArchetypesForCharacter(_currentCharacter);
        if (archsForChar.Count == 0) return;

        if (ActiveArchetype == null)
        {
            SetActiveArchetype(archsForChar[0]);
            return;
        }

        int idx = archsForChar.IndexOf(ActiveArchetype);
        int next = idx + 1;
        if (next >= archsForChar.Count)
            SetActiveArchetype(archsForChar[0]); // wrap within character
        else
            SetActiveArchetype(archsForChar[next]);
    }

    /// <summary>
    /// Calculate the archetype bonus for a card.
    /// Returns 0 if no archetype is active or the card doesn't match.
    /// </summary>
    public static int GetArchetypeBonus(string cardId, string[]? cardTags)
    {
        if (ActiveArchetype == null) return 0;

        int bonus = 0;

        // Key card bonus (highest priority)
        if (ActiveArchetype.KeyCardIds.Contains(cardId))
            bonus += KeyCardBonus;

        // Tag match bonus
        if (cardTags != null)
        {
            foreach (var tag in cardTags)
            {
                if (ActiveArchetype.SynergyTags.Contains(tag))
                {
                    bonus += TagMatchBonus;
                    break; // Only one tag match bonus
                }
            }
        }

        return System.Math.Min(bonus, MaxArchetypeBonus);
    }

    /// <summary>
    /// Get the build-specific rating for a card. Returns the override
    /// rating if the active archetype defines one, otherwise null.
    /// </summary>
    public static int? GetBuildRating(string cardId)
    {
        if (ActiveArchetype == null) return null;
        if (ActiveArchetype.CardRatingOverrides.TryGetValue(cardId, out int rating))
            return rating;
        return null;
    }

    /// <summary>
    /// Check if a card has synergy with the active archetype (for UI display).
    /// </summary>
    public static bool HasSynergy(string cardId, string[]? cardTags)
    {
        if (ActiveArchetype == null) return false;
        if (ActiveArchetype.KeyCardIds.Contains(cardId)) return true;
        if (ActiveArchetype.CardRatingOverrides.ContainsKey(cardId))
        {
            int? buildRating = GetBuildRating(cardId);
            if (buildRating.HasValue && buildRating.Value >= 7) return true;
        }
        if (cardTags != null)
        {
            foreach (var tag in cardTags)
            {
                if (ActiveArchetype.SynergyTags.Contains(tag))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Record a card pick from a reward screen.
    /// </summary>
    public static void OnCardPicked(string cardId, string displayName, int rating)
    {
        bool isSynergy = false;
        if (ActiveArchetype != null)
        {
            var entry = CardDatabase.GetByCardId(cardId) ?? CardDatabase.GetByName(displayName);
            if (entry != null)
                isSynergy = HasSynergy(entry.Id, entry.Tags);
        }

        _pickedCards.Add(new PickedCard
        {
            CardId = cardId,
            DisplayName = displayName,
            Rating = rating,
            IsSynergy = isSynergy
        });

        MainFile.Logger.Info($"[Archetype] Card picked: {displayName} (rating={rating}, synergy={isSynergy})");
    }

    /// <summary>
    /// Clear picked cards (new run).
    /// </summary>
    public static void ClearPickedCards()
    {
        _pickedCards.Clear();
    }
}
