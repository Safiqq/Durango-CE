use std::collections::{BTreeMap, HashMap};
use std::path::Path;

use serde_json::Value;

#[derive(Debug, Clone)]
pub struct GeneratorSpec {
    pub id: String,
    pub prototype_id: String,
    pub name: String,
    pub icon: String,
    pub level: i32,
    pub amount: i32,
    pub effort: f32,
    pub duration: f32,
    pub tool_requirements: BTreeMap<String, i32>,
}

#[derive(Debug, Clone)]
pub struct NaturalSpec {
    pub entity_type: u16,
    pub collectible_id: String,
    pub display_name: String,
    pub generators: Vec<GeneratorSpec>,
}

#[derive(Debug, Clone)]
pub struct RecipeSlotSpec {
    pub slot_id: String,
    pub count_min: usize,
    pub count_max: usize,
    pub required_tags: Vec<String>,
    pub required_materials: Vec<String>,
}

#[derive(Debug, Clone)]
pub struct RecipeSpec {
    pub id: String,
    pub prototype_id: String,
    pub duration: f32,
    pub energy: f32,
    pub skill_category: i32,
    pub slots: Vec<RecipeSlotSpec>,
}

#[derive(Debug, Clone)]
pub struct ArtifactSpec {
    pub entity_type: u16,
    pub blueprint_id: String,
    pub display_name: String,
    pub size_x: u16,
    pub size_y: u16,
    pub height: i32,
    pub can_rest: bool,
    pub is_craft: bool,
    pub is_home: bool,
    pub is_port: bool,
    pub is_burnable: bool,
    pub is_gate: bool,
    pub is_warehouse: bool,
    pub is_washable: bool,
    pub is_warphole: bool,
    pub default_parts: BTreeMap<String, String>,
    pub workbench_tags: Vec<(String, i32)>,
}

#[derive(Debug, Clone)]
pub struct AnimalSpec {
    pub entity_type: u16,
    pub raw_id: String,
    pub name: String,
    pub max_hp: f32,
    pub base_scale: f32,
    pub size_level: i32,
    pub difficulty: f32,
    pub min_island_level: i32,
    pub min_level: i32,
    pub max_level: i32,
    pub is_aggressive: bool,
    pub wild_eligible: bool,
    pub attack_cooltime: f32,
    pub stand_motion: String,
    pub move_motion: String,
    pub dead_motion: String,
    pub battle_stand_motion: String,
    pub attack_motion: String,
    pub attacks: Vec<String>,
    pub attack_damage: i32,
}

#[derive(Debug, Clone)]
pub struct PerformanceTemplate {
    pub id: String,
    pub name: String,
    pub icon: String,
    pub min_level: i32,
    pub max_level: i32,
    pub num_exprs: BTreeMap<String, String>,
    pub strs: BTreeMap<String, String>,
}

#[derive(Debug, Clone)]
pub struct BattleActionSpec {
    pub id: String,
    pub stamina: i32,
    pub cooltime: f32,
    pub damage: i32,
    pub is_dodge: bool,
    pub use_range: f32,
}

#[derive(Debug, Clone)]
pub struct SkillNodeSpec {
    pub category: i32,
    pub skill_id: String,
    pub sub_id: String,
    pub level: i32,
    pub category_level: i32,
    pub skill_point: i32,
    pub untrain_disabled: bool,
    pub rewards: Vec<String>,
}

#[derive(Debug, Clone, Default)]
pub struct SkillRewardSpec {
    pub recipe_ids: Vec<String>,
    pub blueprint_ids: Vec<String>,
}

/// Official Durango cumulative `level_thresholds` from `assets/statistics/player.json`.
/// Index `i` is the cumulative XP required to reach level `i + 2` (so Lv 1 = 0, Lv 2 = 11, Lv 15 = 1784).
pub const DEFAULT_LEVEL_THRESHOLDS: &[i32] = &[
    11, 25, 44, 70, 107, 158, 225, 304, 402, 524, 733, 1006, 1351, 1784, 2324, 2991, 3811, 4812,
    6028, 7498, 9268, 11391, 13926, 16944, 20525, 24760, 29753, 35622, 42501, 50541, 59911, 70804,
    83434, 98043, 114900, 134306, 156594, 182134, 211334, 244643, 282557, 325624, 374446, 429684,
    492062, 562374, 641485, 730338, 829963, 941480, 1066104, 1205147, 1360031, 1532287, 1723567,
    1935648, 2170435, 2430067, 2716826, 3033146,
];

#[derive(Debug, Clone)]
struct ProtoMeta {
    name: String,
    icon: String,
    size: i32,
    category: String,
    eatable: bool,
    tags: Vec<(String, i32)>,
}

pub struct GameCatalog {
    naturals: HashMap<u16, (String, String)>,
    prototypes: HashMap<String, ProtoMeta>,
    gen_meta: HashMap<String, (String, String)>,
    recipes: HashMap<String, RecipeSpec>,
    artifacts_by_type: HashMap<u16, ArtifactSpec>,
    blueprint_to_type: HashMap<String, u16>,
    animals_by_type: HashMap<u16, AnimalSpec>,
    battle_actions: HashMap<String, BattleActionSpec>,
    tag_allow_actions: HashMap<String, Vec<String>>,
    skills_by_key: HashMap<String, HashMap<String, HashMap<i32, SkillNodeSpec>>>,
    free_skill_nodes: Vec<SkillNodeSpec>,
    skill_rewards: HashMap<String, SkillRewardSpec>,
    level_thresholds: Vec<i32>,
    performances_by_proto: HashMap<String, Vec<PerformanceTemplate>>,
}

impl GameCatalog {
    pub fn load(assets_dir: Option<&Path>) -> Self {
        let mut naturals = HashMap::new();
        let mut prototypes = HashMap::new();
        let mut gen_meta = HashMap::new();
        let mut recipes = HashMap::new();
        let mut artifacts_by_type = HashMap::new();
        let mut blueprint_to_type = HashMap::new();
        let mut animals_by_type = HashMap::new();
        let mut battle_actions = HashMap::new();
        let mut tag_allow_actions: HashMap<String, Vec<String>> = HashMap::new();
        let mut skills_by_key: HashMap<String, HashMap<String, HashMap<i32, SkillNodeSpec>>> =
            HashMap::new();
        let mut free_skill_nodes = Vec::new();
        let mut skill_rewards: HashMap<String, SkillRewardSpec> = HashMap::new();
        let mut level_thresholds: Vec<i32> = DEFAULT_LEVEL_THRESHOLDS.to_vec();
        let mut performances_by_proto: HashMap<String, Vec<PerformanceTemplate>> = HashMap::new();
        let en_mo = load_english_mo(assets_dir);

        if let Some(dir) = assets_dir {
            if let Ok(raw) = std::fs::read_to_string(dir.join("entity_types/natural.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (k, v) in map {
                        if let Ok(et) = k.parse::<u16>() {
                            let cid = v["collectible_id"]
                                .as_str()
                                .filter(|s| !s.is_empty())
                                .or_else(|| v["sprite_name"].as_str())
                                .unwrap_or("natural_resource")
                                .to_string();
                            let disp = translate_name(
                                &en_mo,
                                extract_gettext_key(&v["name"]).as_deref(),
                                &cid,
                            );
                            naturals.insert(et, (cid, disp));
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("item/prototype_data.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (proto_id, arr) in map {
                        if let Some(first) = arr.as_array().and_then(|a| a.first()) {
                            let name = translate_name(
                                &en_mo,
                                extract_gettext_key(&first["name"]).as_deref(),
                                &proto_id,
                            );
                            let icon = first["icon"]
                                .as_str()
                                .unwrap_or("icon_nat_wood_log")
                                .to_string();
                            let size = first["size"].as_i64().unwrap_or(1).max(1) as i32;
                            let mut tags = Vec::new();
                            if let Some(tag_obj) = first["tags"].as_object() {
                                for (k, v) in tag_obj {
                                    let lv = v
                                        .as_i64()
                                        .or_else(|| v.as_f64().map(|f| f.round() as i64))
                                        .or_else(|| {
                                            v.as_str().and_then(|s| {
                                                s.parse::<i64>().ok().or_else(|| {
                                                    eval_perf_formula(s, 1)
                                                        .map(|f| f.round() as i64)
                                                })
                                            })
                                        })
                                        .unwrap_or(1)
                                        .max(1) as i32;
                                    tags.push((k.clone(), lv));
                                }
                            }
                            let category = first["category"].as_str().unwrap_or("").to_string();
                            let eatable = tags.iter().any(|(k, _)| k == "eatable")
                                || category.contains("food")
                                || category.contains("medicine");
                            prototypes.insert(
                                proto_id,
                                ProtoMeta {
                                    name,
                                    icon,
                                    size,
                                    category,
                                    eatable,
                                    tags,
                                },
                            );
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("item/generator_client_data.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (gid, obj) in map {
                        let name = translate_name(
                            &en_mo,
                            extract_gettext_key(&obj["name"]).as_deref(),
                            &gid,
                        );
                        let icon = obj["icon"]
                            .as_str()
                            .unwrap_or("icon_nat_wood_log")
                            .to_string();
                        gen_meta.insert(gid, (name, icon));
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("item/recipes.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (rid, obj) in map {
                        if obj["type"].as_i64().unwrap_or(-1) != 0 {
                            continue;
                        }
                        let Some(proto) = obj["prototype_id"]
                            .as_str()
                            .filter(|s| !s.is_empty())
                        else {
                            continue;
                        };
                        let duration = obj["duration"].as_f64().unwrap_or(1.0) as f32;
                        let energy = obj["energy"]
                            .as_str()
                            .and_then(|s| s.parse::<f32>().ok())
                            .or_else(|| obj["energy"].as_f64().map(|v| v as f32))
                            .unwrap_or(5.0);
                        let category_str = obj["category"].as_str().unwrap_or("");
                        let subcat_str = obj["subcategory"].as_str().unwrap_or("");
                        let skill_category = match category_str {
                            "cook" | "cook_season2" => durango_wire::SKILL_CAT_COOKING,
                            "clothing" => durango_wire::SKILL_CAT_ARMORCRAFTING,
                            "material_process" | "process_season2" => {
                                if subcat_str == "process_build" {
                                    durango_wire::SKILL_CAT_CONSTRUCTING
                                } else {
                                    durango_wire::SKILL_CAT_PROCESS
                                }
                            }
                            _ => durango_wire::SKILL_CAT_WEAPONCRAFTING,
                        };
                        let mut slots = Vec::new();
                        if let Some(arr) = obj["slots"].as_array() {
                            for s in arr {
                                let slot_id = s["slot_id"]
                                    .as_str()
                                    .unwrap_or("base")
                                    .to_string();
                                let count_min = s["count_min"].as_u64().unwrap_or(1) as usize;
                                let count_max =
                                    s["count_max"].as_u64().unwrap_or(count_min as u64) as usize;
                                let required_tags = s["required_tags"]
                                    .as_object()
                                    .map(|m| m.keys().cloned().collect())
                                    .unwrap_or_default();
                                let required_materials = s["required_materials"]
                                    .as_object()
                                    .map(|m| m.keys().cloned().collect())
                                    .unwrap_or_default();
                                slots.push(RecipeSlotSpec {
                                    slot_id,
                                    count_min,
                                    count_max: count_max.max(count_min),
                                    required_tags,
                                    required_materials,
                                });
                            }
                        }
                        recipes.insert(
                            rid.clone(),
                            RecipeSpec {
                                id: rid,
                                prototype_id: proto.to_string(),
                                duration,
                                energy,
                                skill_category,
                                slots,
                            },
                        );
                    }
                }
            }
            let mut valid_models: std::collections::HashSet<String> = std::collections::HashSet::new();
            if let Ok(raw) = std::fs::read_to_string(dir.join("building/artifact_models.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for k in map.keys() {
                        valid_models.insert(k.clone());
                    }
                }
            }
            let mut blueprint_parts_map: HashMap<String, BTreeMap<String, String>> = HashMap::new();
            if let Ok(raw) = std::fs::read_to_string(dir.join("building/blueprints.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (bp_id, v) in map {
                        let mut parts = BTreeMap::new();
                        if let Some(slots) = v["slots"].as_array() {
                            for slot in slots {
                                let sid = slot["slot_id"].as_str().unwrap_or("main");
                                let default_tag = slot["default_look_tag"].as_str().unwrap_or("");
                                let model_key = slot["looks"]
                                    .get(default_tag)
                                    .and_then(|l| l["model_key"].as_str())
                                    .or_else(|| {
                                        slot["looks"]
                                            .as_object()
                                            .and_then(|m| m.values().next())
                                            .and_then(|l| l["model_key"].as_str())
                                    });
                                if let Some(mk) = model_key.filter(|s| !s.is_empty()) {
                                    if valid_models.is_empty() || valid_models.contains(mk) {
                                        parts.insert(sid.to_string(), mk.to_string());
                                    }
                                }
                            }
                        }
                        if parts.is_empty() {
                            if let Some(dl) = v["default_look"].as_str().filter(|s| !s.is_empty()) {
                                if valid_models.is_empty() || valid_models.contains(dl) {
                                    // Unity Building.Blueprint.cs:375-377 & Cheats.cs:295 resolve
                                    // default_look via Display.Parts["common"] (e.g. dock -> dock_01_wood,
                                    // bonfire -> bonfire_01_burning).
                                    let burning_key = format!("{dl}_burning");
                                    let chosen = if (bp_id.contains("bonfire")
                                        || bp_id.contains("furnace")
                                        || bp_id.contains("kiln"))
                                        && valid_models.contains(&burning_key)
                                    {
                                        burning_key
                                    } else {
                                        dl.to_string()
                                    };
                                    parts.insert("common".to_string(), chosen);
                                }
                            }
                        }
                        if parts.is_empty() && valid_models.contains(&bp_id) {
                            parts.insert("common".to_string(), bp_id.clone());
                        }
                        if !parts.is_empty() {
                            blueprint_parts_map.insert(bp_id, parts);
                        }
                    }
                }
            }

            let mut workbench_tags_map: HashMap<String, Vec<(String, i32)>> = HashMap::new();
            if let Ok(raw) = std::fs::read_to_string(dir.join("derived/workbench_tags.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (bp_id, v) in map {
                        let mut tags = Vec::new();
                        if let Some(obj) = v.as_object() {
                            for (tid, lv_val) in obj {
                                let lv = lv_val.as_i64().unwrap_or(1).max(1) as i32;
                                tags.push((tid.clone(), lv));
                            }
                        }
                        if !tags.is_empty() {
                            workbench_tags_map.insert(bp_id, tags);
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("entity_types/artifact.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (k, v) in map {
                        if let Ok(et) = k.parse::<u16>() {
                            let bp = v["__name__"]
                                .as_str()
                                .filter(|s| !s.is_empty())
                                .map(str::to_string)
                                .unwrap_or_else(|| format!("artifact_{et}"));
                            let display_name = translate_name(
                                &en_mo,
                                extract_gettext_key(&v["name"]).as_deref(),
                                &bp,
                            );
                            let size_x = v["size"][0].as_u64().unwrap_or(1).max(1) as u16;
                            let size_y = v["size"][1].as_u64().unwrap_or(1).max(1) as u16;
                            let height = v["height"].as_i64().unwrap_or(1).max(1) as i32;
                            let comps_arr = v["components"]
                                .as_array()
                                .or_else(|| v["Components"].as_array());
                            let has_comp = |needle: &str| {
                                comps_arr.is_some_and(|arr| {
                                    arr.iter()
                                        .any(|c| c.as_str().is_some_and(|s| s.eq_ignore_ascii_case(needle)))
                                })
                            };
                            let comps_rest =
                                has_comp("Shelter") || has_comp("Rest") || has_comp("Bed");
                            let is_craft = v["is_craft"].as_bool().unwrap_or(false)
                                || has_comp("Workbench");
                            let is_home = has_comp("Home");
                            let is_port = et == 7001 || bp == "dock" || has_comp("Port");
                            let is_burnable = has_comp("Burnable")
                                || bp.contains("bonfire")
                                || bp.contains("kiln")
                                || bp.contains("furnace");
                            let is_gate = has_comp("Gate") || bp.contains("gate");
                            let is_warehouse = has_comp("Warehouse")
                                || has_comp("ClanWarehouse")
                                || has_comp("Inventory")
                                || bp.contains("box")
                                || bp.contains("basket")
                                || bp.contains("warehouse");
                            let is_washable = has_comp("Washable") || bp.contains("well");
                            let is_warphole = et == 9450
                                || bp.contains("warphole")
                                || has_comp("Warphole")
                                || has_comp("PersonalRegionWarphole")
                                || has_comp("CargoWarphole")
                                || has_comp("NeutralCargoWarphole");
                            let can_rest = comps_rest
                                || is_home
                                || bp.to_ascii_lowercase().contains("tent")
                                || bp.to_ascii_lowercase().contains("bed")
                                || bp.to_ascii_lowercase().contains("chair")
                                || bp.to_ascii_lowercase().contains("bonfire");
                            let default_parts = blueprint_parts_map
                                .remove(&bp)
                                .unwrap_or_else(|| {
                                    let mut m = BTreeMap::new();
                                    if valid_models.contains(&bp) {
                                        m.insert("common".to_string(), bp.clone());
                                    }
                                    m
                                });
                            let workbench_tags = workbench_tags_map
                                .remove(&bp)
                                .unwrap_or_else(|| fallback_workbench_tags(&bp));
                            blueprint_to_type.insert(bp.clone(), et);
                            artifacts_by_type.insert(
                                et,
                                ArtifactSpec {
                                    entity_type: et,
                                    blueprint_id: bp,
                                    display_name,
                                    size_x,
                                    size_y,
                                    height,
                                    can_rest,
                                    is_craft,
                                    is_home,
                                    is_port,
                                    is_burnable,
                                    is_gate,
                                    is_warehouse,
                                    is_washable,
                                    is_warphole,
                                    default_parts,
                                    workbench_tags,
                                },
                            );
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("performance.json")) {
                if let Ok(Value::Object(cats)) = serde_json::from_str::<Value>(&raw) {
                    for (perf_id, proto_map) in cats {
                        let Some(proto_obj) = proto_map.as_object() else {
                            continue;
                        };
                        for (proto_id, ranges) in proto_obj {
                            let Some(range_obj) = ranges.as_object() else {
                                continue;
                            };
                            for (range_key, spec_val) in range_obj {
                                let Some(spec_obj) = spec_val.as_object() else {
                                    continue;
                                };
                                let (min_lv, max_lv) = parse_level_range_key(range_key);
                                let name = translate_name(
                                    &en_mo,
                                    spec_obj
                                        .get("name")
                                        .and_then(extract_gettext_key)
                                        .as_deref(),
                                    &perf_id,
                                );
                                let icon = spec_obj
                                    .get("icon")
                                    .and_then(Value::as_str)
                                    .unwrap_or("")
                                    .to_string();
                                let mut num_exprs = BTreeMap::new();
                                let mut strs = BTreeMap::new();
                                for (fk, fv) in spec_obj {
                                    if fk == "name" || fk == "icon" {
                                        continue;
                                    }
                                    match fv {
                                        Value::Number(n) => {
                                            if let Some(f) = n.as_f64() {
                                                num_exprs.insert(fk.clone(), format!("{f}"));
                                            }
                                        }
                                        Value::String(s) => {
                                            let trimmed = s.trim();
                                            if looks_like_numeric_formula(trimmed) {
                                                num_exprs.insert(fk.clone(), trimmed.to_string());
                                            } else if !trimmed.is_empty() && trimmed != "None" {
                                                strs.insert(fk.clone(), trimmed.to_string());
                                            }
                                        }
                                        _ => {}
                                    }
                                }
                                performances_by_proto
                                    .entry(proto_id.clone())
                                    .or_default()
                                    .push(PerformanceTemplate {
                                        id: perf_id.clone(),
                                        name,
                                        icon,
                                        min_level: min_lv,
                                        max_level: max_lv,
                                        num_exprs,
                                        strs,
                                    });
                            }
                        }
                    }
                }
            }
            let mut animal_motions: HashMap<
                u16,
                (String, String, String, String, String, Vec<String>),
            > = HashMap::new();
            if let Ok(raw) =
                std::fs::read_to_string(dir.join("derived/animal_motions.json"))
            {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (k, v) in map {
                        if let Ok(et) = k.parse::<u16>() {
                            let stand = v["stand"]
                                .as_str()
                                .or_else(|| v["idle"].as_str())
                                .or_else(|| v["battle_stand"].as_str())
                                .unwrap_or("Stand")
                                .to_string();
                            let mv = v["move"]
                                .as_str()
                                .unwrap_or(&stand)
                                .to_string();
                            let dead = v["dead"]
                                .as_str()
                                .unwrap_or(&stand)
                                .to_string();
                            let battle_stand = v["battle_stand"]
                                .as_str()
                                .or_else(|| v["battle_idle"].as_str())
                                .unwrap_or(&stand)
                                .to_string();
                            let mut attacks: Vec<String> = v["attacks"]
                                .as_array()
                                .map(|arr| {
                                    arr.iter()
                                        .filter_map(|x| x.as_str().map(str::to_string))
                                        .collect()
                                })
                                .unwrap_or_default();
                            let attack = v["attack_normal"]
                                .as_str()
                                .map(str::to_string)
                                .or_else(|| attacks.first().cloned())
                                .unwrap_or_else(|| battle_stand.clone());
                            if attacks.is_empty() {
                                attacks.push(attack.clone());
                            }
                            animal_motions
                                .insert(et, (stand, mv, dead, battle_stand, attack, attacks));
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("entity_types/animal.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (k, v) in map {
                        if let Ok(et) = k.parse::<u16>() {
                            let raw_id = v["__name__"]
                                .as_str()
                                .filter(|s| !s.is_empty())
                                .map(str::to_string)
                                .unwrap_or_else(|| format!("animal_{et}"));
                            let name = translate_name(
                                &en_mo,
                                extract_gettext_key(&v["name"]).as_deref(),
                                &raw_id,
                            );
                            let base_scale = v["represent_scale"]
                                .as_f64()
                                .map(|f| f as f32)
                                .filter(|&f| f > 0.1)
                                .unwrap_or(1.0);
                            let size_level = v["size_level"].as_i64().unwrap_or(2).clamp(1, 7) as i32;
                            let difficulty = v["difficulty"].as_f64().unwrap_or(1.0) as f32;
                            let rep_lv = v["represent_combat_level"].as_i64().unwrap_or(1).clamp(1, 60) as i32;
                            let min_level = v["combat_level_ranges"][0]
                                .as_i64()
                                .unwrap_or(rep_lv as i64)
                                .max(rep_lv as i64)
                                .clamp(1, 60) as i32;
                            let max_level = v["combat_level_ranges"][1]
                                .as_i64()
                                .unwrap_or(min_level as i64)
                                .clamp(min_level as i64, 60) as i32;
                            let kind_str = v["type"].as_str().unwrap_or("");
                            let raw_lower = raw_id.to_ascii_lowercase();
                            let wild_eligible = ![
                                "_for_pet",
                                "_xmas",
                                "_witch",
                                "_hanbok",
                                "_paper",
                                "_unknown",
                                "_season",
                                "watermelon",
                                "dodo",
                                "_elite",
                                "_event",
                                "_boss",
                                "_test",
                                "dummy",
                                "sandbag",
                                "_baby",
                                "_junior",
                                "recruiter_",
                                "retriever_",
                                "_factor",
                            ]
                            .iter()
                            .any(|bad| raw_lower.contains(bad));
                            let is_aggressive = !raw_lower.contains("coward")
                                && !raw_lower.contains("timid")
                                && (kind_str.eq_ignore_ascii_case("carnivore")
                                    || kind_str.eq_ignore_ascii_case("predator")
                                    || raw_lower.contains("raptor")
                                    || raw_lower.contains("wolf")
                                    || raw_lower.contains("smilodon")
                                    || raw_lower.contains("sabertooth")
                                    || raw_lower.contains("allosaur")
                                    || raw_lower.contains("carno")
                                    || raw_lower.contains("tarbo")
                                    || raw_lower.contains("tyranno")
                                    || raw_lower.contains("rex")
                                    || raw_lower.contains("spino")
                                    || raw_lower.contains("deinonychus")
                                    || raw_lower.contains("dilopho")
                                    || raw_lower.contains("andrewsarchus"));
                            let is_giant_or_apex = size_level >= 5
                                || difficulty >= 8.0
                                || raw_lower.contains("brachio")
                                || raw_lower.contains("amarga")
                                || raw_lower.contains("diplo")
                                || raw_lower.contains("tyranno")
                                || raw_lower.contains("rex")
                                || raw_lower.contains("spino")
                                || raw_lower.contains("mammoth");
                            let is_heavy_mid = size_level >= 4
                                || difficulty >= 3.5
                                || raw_lower.contains("tricera")
                                || raw_lower.contains("ankylo")
                                || raw_lower.contains("stego")
                                || raw_lower.contains("carno")
                                || raw_lower.contains("tarbo")
                                || raw_lower.contains("sabertooth")
                                || raw_lower.contains("smilodon")
                                || raw_lower.contains("megaloceros");
                            let min_island_level = if is_giant_or_apex {
                                45
                            } else if is_heavy_mid {
                                25
                            } else if size_level >= 3 || difficulty >= 2.0 || is_aggressive {
                                15
                            } else {
                                1
                            }
                            .max(min_level);
                            let attack_cooltime = v["attack_cooltime"]
                                .as_f64()
                                .map(|f| (f as f32).clamp(1.2, 5.0))
                                .unwrap_or(2.2);
                            let max_hp = v["life_max"]
                                .as_f64()
                                .map(|f| f as f32)
                                .or_else(|| {
                                    v["life_max"]
                                        .as_str()
                                        .and_then(|s| eval_perf_formula(s, min_level))
                                })
                                .or_else(|| {
                                    v["body_parts"]["body"]["max_hp"].as_f64().map(|f| f as f32)
                                })
                                .filter(|&f| f >= 10.0)
                                .map(|f| f.clamp(20.0, 1500.0))
                                .unwrap_or(60.0);
                            let attack_damage = v["attack"]
                                .as_f64()
                                .map(|f| f.round() as i32)
                                .or_else(|| {
                                    v["attack"].as_str().and_then(|s| {
                                        eval_perf_formula(s, min_level).map(|f| f.round() as i32)
                                    })
                                })
                                .unwrap_or_else(|| (max_hp * 0.22).round() as i32)
                                .clamp(8, 45);
                            let (
                                stand_motion,
                                move_motion,
                                dead_motion,
                                battle_stand_motion,
                                attack_motion,
                                attacks,
                            ) = animal_motions.remove(&et).unwrap_or_else(|| {
                                (
                                    "Para_Stand".into(),
                                    "Para_Walk".into(),
                                    "Para_Die".into(),
                                    "Para_BattleStand".into(),
                                    "Para_Attack_Stamp".into(),
                                    vec!["Para_Attack_Stamp".into()],
                                )
                            });
                            animals_by_type.insert(
                                et,
                                AnimalSpec {
                                    entity_type: et,
                                    raw_id,
                                    name,
                                    max_hp,
                                    base_scale,
                                    size_level,
                                    difficulty,
                                    min_island_level,
                                    min_level,
                                    max_level,
                                    is_aggressive,
                                    wild_eligible,
                                    attack_cooltime,
                                    stand_motion,
                                    move_motion,
                                    dead_motion,
                                    battle_stand_motion,
                                    attack_motion,
                                    attacks,
                                    attack_damage,
                                },
                            );
                        }
                    }
                }
            }

            if let Ok(raw) =
                std::fs::read_to_string(dir.join("player/player_battle_actions.json"))
            {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (aid, v) in map {
                        let stamina = v["meta"]["stamina"].as_i64().unwrap_or(10) as i32;
                        let cooltime = v["meta"]["cooltime"].as_f64().unwrap_or(0.8) as f32;
                        let has_atk = v["attack_info"]
                            .as_array()
                            .is_some_and(|a| !a.is_empty());
                        let bonus = v["attack_info"][0]["damage_bonus"]
                            .as_f64()
                            .unwrap_or(1.0) as f32;
                        let is_dodge = !has_atk || aid.ends_with("_dodge");
                        let damage = if is_dodge {
                            0
                        } else {
                            ((30.0 * bonus).round() as i32).max(15)
                        };
                        let is_ranged = aid.starts_with("ranged_");
                        let default_range = if is_ranged { 2400.0 } else { 600.0 };
                        let use_range = v["meta"]["use_range"]
                            .as_f64()
                            .map(|r| {
                                if is_ranged {
                                    (r as f32).max(default_range)
                                } else {
                                    ((r as f32) + 220.0).clamp(320.0, 600.0)
                                }
                            })
                            .unwrap_or(default_range);
                        battle_actions.insert(
                            aid.clone(),
                            BattleActionSpec {
                                id: aid,
                                stamina,
                                cooltime,
                                damage,
                                is_dodge,
                                use_range,
                            },
                        );
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("tag_allow_actions.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (tag_id, v) in map {
                        let mut list = Vec::new();
                        for field in ["default_actions", "skill_actions"] {
                            if let Some(arr) = v[field].as_array() {
                                for item in arr {
                                    if let Some(aid) = item.as_str() {
                                        if !list.iter().any(|x| x == aid) {
                                            list.push(aid.to_string());
                                        }
                                    }
                                }
                            }
                        }
                        if !list.is_empty() {
                            tag_allow_actions.insert(tag_id, list);
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("skill/skills.json")) {
                if let Ok(Value::Object(cat_map)) = serde_json::from_str::<Value>(&raw) {
                    for (cat_key, cat_val) in cat_map {
                        let Ok(cat_id) = cat_key.parse::<i32>() else {
                            continue;
                        };
                        let Some(skills_obj) = cat_val.as_object() else {
                            continue;
                        };
                        for (skill_id, sub_val) in skills_obj {
                            let Some(subs_obj) = sub_val.as_object() else {
                                continue;
                            };
                            for (sub_id, levels_val) in subs_obj {
                                let Some(levels_arr) = levels_val.as_array() else {
                                    continue;
                                };
                                for (idx, entry) in levels_arr.iter().enumerate() {
                                    let level = (idx as i32) + 1;
                                    let category = entry["category"]
                                        .as_i64()
                                        .map(|v| v as i32)
                                        .unwrap_or(cat_id);
                                    let category_level = entry["category_level"]
                                        .as_i64()
                                        .unwrap_or(1)
                                        as i32;
                                    let skill_point = entry["skill_point"]
                                        .as_i64()
                                        .unwrap_or(1)
                                        .max(0)
                                        as i32;
                                    let untrain_disabled = entry["untrain_disabled"]
                                        .as_bool()
                                        .unwrap_or(false);
                                    let rewards: Vec<String> = entry["rewards"]
                                        .as_array()
                                        .map(|arr| {
                                            arr.iter()
                                                .filter_map(|v| v.as_str().map(str::to_string))
                                                .collect()
                                        })
                                        .unwrap_or_default();
                                    let node = SkillNodeSpec {
                                        category,
                                        skill_id: skill_id.clone(),
                                        sub_id: sub_id.clone(),
                                        level,
                                        category_level,
                                        skill_point,
                                        untrain_disabled,
                                        rewards,
                                    };
                                    if skill_point == 0 {
                                        free_skill_nodes.push(node.clone());
                                    }
                                    skills_by_key
                                        .entry(skill_id.clone())
                                        .or_default()
                                        .entry(sub_id.clone())
                                        .or_default()
                                        .insert(level, node);
                                }
                            }
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("skill/rewards.json")) {
                if let Ok(Value::Object(map)) = serde_json::from_str::<Value>(&raw) {
                    for (rid, obj) in map {
                        let recipe_ids: Vec<String> = obj["recipe_ids"]
                            .as_array()
                            .map(|arr| {
                                arr.iter()
                                    .filter_map(|v| v.as_str().map(str::to_string))
                                    .collect()
                            })
                            .unwrap_or_default();
                        let blueprint_ids: Vec<String> = obj["blueprint_ids"]
                            .as_array()
                            .map(|arr| {
                                arr.iter()
                                    .filter_map(|v| v.as_str().map(str::to_string))
                                    .collect()
                            })
                            .unwrap_or_default();
                        if !recipe_ids.is_empty() || !blueprint_ids.is_empty() {
                            skill_rewards.insert(
                                rid,
                                SkillRewardSpec {
                                    recipe_ids,
                                    blueprint_ids,
                                },
                            );
                        }
                    }
                }
            }

            if let Ok(raw) = std::fs::read_to_string(dir.join("statistics/player.json")) {
                if let Ok(obj) = serde_json::from_str::<Value>(&raw) {
                    if let Some(arr) = obj["level_thresholds"].as_array() {
                        let parsed: Vec<i32> = arr
                            .iter()
                            .filter_map(|v| v.as_i64().map(|n| n as i32))
                            .collect();
                        if parsed.len() >= 60 {
                            level_thresholds = parsed;
                        }
                    }
                }
            }
        }

        // Built-in starter fallbacks for headless unit tests
        recipes.entry("stone_knife".into()).or_insert_with(|| RecipeSpec {
            id: "stone_knife".into(),
            prototype_id: "tool_stone_knife".into(),
            duration: 1.0,
            energy: 5.0,
            skill_category: durango_wire::SKILL_CAT_WEAPONCRAFTING,
            slots: vec![RecipeSlotSpec {
                slot_id: "material".into(),
                count_min: 1,
                count_max: 2,
                required_tags: Vec::new(),
                required_materials: Vec::new(),
            }],
        });
        blueprint_to_type.entry("tent".into()).or_insert(6151);
        artifacts_by_type.entry(6151).or_insert_with(|| ArtifactSpec {
            entity_type: 6151,
            blueprint_id: "tent".into(),
            display_name: "Camp Tent".into(),
            size_x: 2,
            size_y: 2,
            height: 1,
            can_rest: true,
            is_craft: true,
            is_home: true,
            is_port: false,
            is_burnable: false,
            is_gate: false,
            is_warehouse: false,
            is_washable: false,
            is_warphole: false,
            default_parts: BTreeMap::from([("main".into(), "tent".into())]),
            workbench_tags: Vec::new(),
        });
        blueprint_to_type.entry("dock".into()).or_insert(7001);
        artifacts_by_type.entry(7001).or_insert_with(|| ArtifactSpec {
            entity_type: 7001,
            blueprint_id: "dock".into(),
            display_name: "Harbor".into(),
            size_x: 3,
            size_y: 3,
            height: 1,
            can_rest: false,
            is_craft: false,
            is_home: false,
            is_port: true,
            is_burnable: false,
            is_gate: false,
            is_warehouse: false,
            is_washable: false,
            is_warphole: false,
            default_parts: BTreeMap::from([("common".into(), "dock_01_wood".into())]),
            workbench_tags: Vec::new(),
        });
        for (et, name, hp, sz, aggro, cd, stand, mv, dead, bstand, atk, dmg) in [
            (
                2048u16,
                "iguanodon",
                60.0f32,
                4i32,
                false,
                2.2f32,
                "Para_Stand",
                "Para_Walk",
                "Para_Die",
                "Para_BattleStand",
                "Para_Attack_Stamp",
                14i32,
            ),
            (
                2051u16,
                "raptor_coward",
                50.0f32,
                2i32,
                false,
                1.7f32,
                "Raptor_Stand",
                "Raptor_Walk",
                "Raptor_Die",
                "Raptor_BattleStand",
                "Raptor_Attack",
                12i32,
            ),
            (
                2001u16,
                "raptor",
                55.0f32,
                2i32,
                true,
                1.7f32,
                "Raptor_Stand",
                "Raptor_Walk",
                "Raptor_Die",
                "Raptor_BattleStand",
                "Raptor_Attack",
                15i32,
            ),
            (
                2056u16,
                "macrauchenia",
                70.0f32,
                2i32,
                false,
                2.5f32,
                "Macrau_Stand",
                "Macrau_Walk",
                "Macrau_Die",
                "Macrau_BattleStand",
                "Macrau_Attack_Kick",
                16i32,
            ),
        ] {
            animals_by_type.entry(et).or_insert_with(|| AnimalSpec {
                entity_type: et,
                raw_id: name.into(),
                name: name.into(),
                max_hp: hp,
                base_scale: 1.0,
                size_level: sz,
                difficulty: if aggro { 2.0 } else { 1.0 },
                min_island_level: if aggro { 15 } else { 1 },
                min_level: 1,
                max_level: 10,
                is_aggressive: aggro,
                wild_eligible: true,
                attack_cooltime: cd,
                stand_motion: stand.into(),
                move_motion: mv.into(),
                dead_motion: dead.into(),
                battle_stand_motion: bstand.into(),
                attack_motion: atk.into(),
                attacks: vec![atk.into()],
                attack_damage: dmg,
            });
        }
        for (id, stamina, cooltime, damage, is_dodge, use_range) in [
            ("barehand_default_a", 10, 0.8f32, 30, false, 600.0f32),
            ("barehand_default_b", 10, 0.8f32, 30, false, 600.0f32),
            ("barehand_kick_a", 12, 1.5f32, 24, false, 600.0f32),
            ("barehand_kick_b", 12, 1.5f32, 24, false, 600.0f32),
            ("barehand_smash", 18, 3.0f32, 45, false, 600.0f32),
            ("barehand_combination", 20, 4.0f32, 50, false, 600.0f32),
            ("barehand_dodge", 35, 1.0f32, 0, true, 600.0f32),
            ("melee_tackle", 15, 3.0f32, 30, false, 600.0f32),
            ("onehand_default_a", 12, 0.9f32, 45, false, 600.0f32),
            ("onehand_default_b", 12, 0.9f32, 45, false, 600.0f32),
            ("onehand_default_c", 12, 0.9f32, 45, false, 600.0f32),
            ("onehand_dodge", 35, 1.0f32, 0, true, 600.0f32),
            ("onehand_smash", 20, 3.5f32, 65, false, 600.0f32),
            ("onehand_flurry", 22, 4.0f32, 55, false, 600.0f32),
            ("onehand_stab", 18, 3.0f32, 55, false, 600.0f32),
        ] {
            battle_actions
                .entry(id.into())
                .or_insert_with(|| BattleActionSpec {
                    id: id.into(),
                    stamina,
                    cooltime,
                    damage,
                    is_dodge,
                    use_range,
                });
        }
        tag_allow_actions.entry("bare_hands".into()).or_insert_with(|| {
            vec![
                "barehand_default_a".into(),
                "barehand_default_b".into(),
                "barehand_dodge".into(),
                "barehand_kick_a".into(),
                "barehand_kick_b".into(),
                "barehand_smash".into(),
                "barehand_combination".into(),
                "melee_tackle".into(),
            ]
        });
        tag_allow_actions
            .entry("sword_onehand".into())
            .or_insert_with(|| {
                vec![
                    "onehand_default_a".into(),
                    "onehand_default_b".into(),
                    "onehand_default_c".into(),
                    "onehand_dodge".into(),
                    "onehand_smash".into(),
                    "onehand_flurry".into(),
                    "onehand_stab".into(),
                    "melee_tackle".into(),
                ]
            });

        if !naturals.is_empty() || recipes.len() > 1 || artifacts_by_type.len() > 1 {
            tracing::info!(
                naturals = naturals.len(),
                prototypes = prototypes.len(),
                generators = gen_meta.len(),
                recipes = recipes.len(),
                artifacts = artifacts_by_type.len(),
                animals = animals_by_type.len(),
                battle_actions = battle_actions.len(),
                tag_allow_actions = tag_allow_actions.len(),
                skills = skills_by_key
                    .values()
                    .map(|subs| subs.values().map(HashMap::len).sum::<usize>())
                    .sum::<usize>(),
                free_skills = free_skill_nodes.len(),
                skill_rewards = skill_rewards.len(),
                level_thresholds = level_thresholds.len(),
                "loaded game gathering, crafting, building, wildlife & skill catalog from assets"
            );
        }

        Self {
            naturals,
            prototypes,
            gen_meta,
            recipes,
            artifacts_by_type,
            blueprint_to_type,
            animals_by_type,
            battle_actions,
            tag_allow_actions,
            skills_by_key,
            free_skill_nodes,
            skill_rewards,
            level_thresholds,
            performances_by_proto,
        }
    }

    pub fn has_loaded_skills(&self) -> bool {
        !self.skills_by_key.is_empty()
    }

    pub fn free_skill_nodes(&self) -> &[SkillNodeSpec] {
        &self.free_skill_nodes
    }

    /// Official cumulative XP required to reach `level` (Lv 1 = 0, Lv 2 = 11, Lv 15 = 1784).
    pub fn exp_for_level(&self, level: i32) -> i32 {
        if level <= 1 {
            return 0;
        }
        let idx = (level - 2) as usize;
        self.level_thresholds
            .get(idx)
            .copied()
            .or_else(|| self.level_thresholds.last().copied())
            .unwrap_or(0)
    }

    /// Official level corresponding to cumulative `exp` (capped at 60).
    pub fn level_from_exp(&self, exp: i32) -> i32 {
        let mut lv = 1i32;
        for &threshold in self.level_thresholds.iter().take(59) {
            if exp >= threshold {
                lv += 1;
            } else {
                break;
            }
        }
        lv.clamp(1, 60)
    }

    /// Cumulative XP cap at Lv 60.
    pub fn exp_cap(&self) -> i32 {
        self.exp_for_level(60)
    }

    /// Action-weighted XP grant matching C# `Player.Skills.cs` (`~40 weighted actions per level`).
    pub fn exp_per_action(&self, current_level: i32, weight: i32) -> i32 {
        let lv = current_level.clamp(1, 59);
        let span = (self.exp_for_level(lv + 1) - self.exp_for_level(lv)).max(11);
        let per_unit = (span / 40).max(1);
        per_unit.saturating_mul(weight.max(1))
    }

    /// Resolves all recipe IDs unlocked by the player's learned skills (via `rewards.json`),
    /// always including starter fallback recipes when running without full assets.
    pub fn unlocked_recipe_ids(&self, learned: &[(String, String, i32)]) -> Vec<String> {
        let mut set = std::collections::BTreeSet::new();
        for (skill_id, sub_id, max_lv) in learned {
            let norm_sub = if sub_id.is_empty() { "__base__" } else { sub_id.as_str() };
            for lv in 1..=(*max_lv) {
                if let Some(node) = self
                    .skills_by_key
                    .get(skill_id.as_str())
                    .and_then(|subs| subs.get(norm_sub))
                    .and_then(|lvs| lvs.get(&lv))
                {
                    for rid in &node.rewards {
                        if let Some(rew) = self.skill_rewards.get(rid) {
                            for rec_id in &rew.recipe_ids {
                                if self.recipes.contains_key(rec_id) {
                                    set.insert(rec_id.clone());
                                }
                            }
                        }
                    }
                }
            }
        }
        if !self.has_loaded_skills() {
            for rid in self.recipes.keys() {
                set.insert(rid.clone());
            }
        }
        set.into_iter().collect()
    }

    /// Resolves all craftable building blueprint IDs unlocked by the player's learned skills
    /// (matching C# `Player.cs:HandleGetArtifactBlueprintsMsg`: `is_craft && unlocked`).
    pub fn unlocked_blueprint_ids(&self, learned: &[(String, String, i32)]) -> Vec<String> {
        let mut unlocked = std::collections::HashSet::new();
        for (skill_id, sub_id, max_lv) in learned {
            let norm_sub = if sub_id.is_empty() { "__base__" } else { sub_id.as_str() };
            for lv in 1..=(*max_lv) {
                if let Some(node) = self
                    .skills_by_key
                    .get(skill_id.as_str())
                    .and_then(|subs| subs.get(norm_sub))
                    .and_then(|lvs| lvs.get(&lv))
                {
                    for rid in &node.rewards {
                        if let Some(rew) = self.skill_rewards.get(rid) {
                            for bp_id in &rew.blueprint_ids {
                                unlocked.insert(bp_id.clone());
                            }
                        }
                    }
                }
            }
        }
        let mut out = std::collections::BTreeSet::new();
        if !self.has_loaded_skills() {
            for spec in self.artifacts_by_type.values() {
                if spec.is_craft {
                    out.insert(spec.blueprint_id.clone());
                }
            }
        } else {
            for spec in self.artifacts_by_type.values() {
                if spec.is_craft && unlocked.contains(&spec.blueprint_id) {
                    out.insert(spec.blueprint_id.clone());
                }
            }
        }
        out.into_iter().collect()
    }

    pub fn find_skill_node(&self, skill_id: &str, sub_id: &str, level: i32) -> Option<SkillNodeSpec> {
        let norm_sub = if sub_id.is_empty() { "__base__" } else { sub_id };
        self.skills_by_key
            .get(skill_id)
            .and_then(|subs| subs.get(norm_sub))
            .and_then(|lvs| lvs.get(&level))
            .cloned()
    }

    pub fn resolve_skill_node(&self, skill_id: &str, sub_id: &str, level: i32) -> SkillNodeSpec {
        let norm_sub = if sub_id.is_empty() { "__base__" } else { sub_id };
        if let Some(spec) = self
            .skills_by_key
            .get(skill_id)
            .and_then(|subs| subs.get(norm_sub))
            .and_then(|lvs| lvs.get(&level))
        {
            return spec.clone();
        }
        // Fallback for minimal test runs without assets_dir
        let cat = if skill_id.contains("gather") {
            7
        } else if skill_id.contains("construct") {
            12
        } else if skill_id.contains("butcher") {
            5
        } else if skill_id.contains("weapon") || skill_id.contains("craft") {
            9
        } else {
            0
        };
        SkillNodeSpec {
            category: cat,
            skill_id: skill_id.to_string(),
            sub_id: norm_sub.to_string(),
            level: level.max(1),
            category_level: 1,
            skill_point: 1,
            untrain_disabled: false,
            rewards: Vec::new(),
        }
    }

    pub fn resolve_natural(&self, entity_type: u16) -> NaturalSpec {
        let (collectible_id, display_name) = self
            .naturals
            .get(&entity_type)
            .cloned()
            .unwrap_or_else(|| {
                let id = format!("natural_{entity_type}");
                let disp = humanize_id(&id);
                (id, disp)
            });

        let lower = collectible_id.to_ascii_lowercase();
        let has_token = |tok: &str| lower.split('_').any(|t| t == tok);
        let proto_ids: Vec<&str> = if lower.contains("ore_iron") {
            vec!["ore_iron", "stone"]
        } else if lower.contains("ore_copper") {
            vec!["ore_copper", "stone"]
        } else if lower.contains("sulphur") || lower.contains("sulfur") {
            vec!["sulphur", "stone"]
        } else if lower.contains("clay") || lower.contains("mud") {
            vec!["mud", "stone"]
        } else if has_token("sand") || lower.contains("sandstone") || lower.contains("sand_pile") {
            vec!["sand", "stone"]
        } else if lower.contains("salt") {
            vec!["salt", "stone"]
        } else if has_token("ore") || has_token("rock") || has_token("stone") || has_token("boulder") || has_token("mineral") {
            vec!["stone", "ore_iron"]
        } else if lower.contains("berry") || lower.contains("fruit") || lower.contains("shrub") {
            vec!["fruit_berry", "leaf_small"]
        } else if lower.contains("reed") || lower.contains("grass") || lower.contains("flax") {
            vec!["stem_fiber", "leaf_small"]
        } else if lower.contains("tree") || lower.contains("wood") || lower.contains("bush") || lower.contains("forest") {
            vec!["wood_bough", "wood_log", "leaf_small"]
        } else {
            vec!["wood_bough", "stone"]
        };

        let generators = proto_ids
            .into_iter()
            .enumerate()
            .map(|(idx, proto)| {
                let (name, icon) = self
                    .gen_meta
                    .get(proto)
                    .cloned()
                    .or_else(|| {
                        self.prototypes
                            .get(proto)
                            .map(|m| (m.name.clone(), m.icon.clone()))
                    })
                    .unwrap_or_else(|| (humanize_id(proto), default_icon_for(proto).to_string()));
                GeneratorSpec {
                    id: proto.to_string(),
                    prototype_id: proto.to_string(),
                    name,
                    icon,
                    level: 1,
                    amount: if idx == 0 { 3 } else { 2 },
                    effort: 10.0,
                    duration: 1.5,
                    tool_requirements: default_tool_requirements(proto),
                }
            })
            .collect();

        NaturalSpec {
            entity_type,
            display_name,
            collectible_id,
            generators,
        }
    }

    pub fn prototype_info(&self, prototype_id: &str) -> (String, String, i32) {
        if let Some(m) = self.prototypes.get(prototype_id) {
            return (m.name.clone(), m.icon.clone(), m.size);
        }
        (
            humanize_id(prototype_id),
            default_icon_for(prototype_id).to_string(),
            1,
        )
    }

    pub fn prototype_tags(&self, prototype_id: &str) -> Vec<(String, i32)> {
        if let Some(m) = self.prototypes.get(prototype_id) {
            if !m.tags.is_empty() {
                return m.tags.clone();
            }
        }
        let lower = prototype_id.to_ascii_lowercase();
        let mut tags = Vec::new();
        if lower.contains("knife") {
            tags.push(("knife".into(), 1));
            tags.push(("blade".into(), 1));
            tags.push(("tool".into(), 1));
        } else if lower.contains("axe") && !lower.contains("pickaxe") {
            tags.push(("axe".into(), 1));
            tags.push(("blade".into(), 1));
            tags.push(("tool".into(), 1));
        } else if lower.contains("pickaxe") {
            tags.push(("pickaxe".into(), 1));
            tags.push(("tool".into(), 1));
        } else if lower.contains("hammer") {
            tags.push(("hammer".into(), 1));
            tags.push(("tool".into(), 1));
        } else if lower.contains("saw") {
            tags.push(("saw".into(), 1));
            tags.push(("blade".into(), 1));
            tags.push(("tool".into(), 1));
        } else if lower.contains("needle") {
            tags.push(("needle".into(), 1));
            tags.push(("tool".into(), 1));
        } else if lower.contains("bow") {
            tags.push(("bow".into(), 1));
            tags.push(("weapon".into(), 1));
        } else if lower.contains("spear") {
            tags.push(("spear".into(), 1));
            tags.push(("weapon".into(), 1));
        } else if lower.contains("sword") {
            tags.push(("sword".into(), 1));
            tags.push(("blade".into(), 1));
            tags.push(("weapon".into(), 1));
        } else if lower.contains("berry") || lower.contains("fruit") {
            tags.push(("fruit".into(), 1));
            tags.push(("eatable".into(), 1));
        } else if lower.contains("leaf") {
            tags.push(("leaf".into(), 1));
            tags.push(("burnable".into(), 1));
        } else if lower.contains("bough") || lower.contains("stick") {
            tags.push(("wood".into(), 1));
            tags.push(("stick_normal".into(), 1));
            tags.push(("burnable".into(), 1));
        } else if lower.contains("log") || lower.contains("wood") {
            tags.push(("wood".into(), 1));
            tags.push(("pillar_normal".into(), 1));
            tags.push(("burnable".into(), 1));
        } else if lower.contains("fiber") || lower.contains("stem") || lower.contains("reed") {
            tags.push(("stalk".into(), 1));
            tags.push(("fiber".into(), 1));
        } else if lower.contains("stone") || lower.contains("rock") {
            tags.push(("stone".into(), 1));
            tags.push(("chunk_normal".into(), 1));
        } else if lower.contains("ore") {
            tags.push(("ore".into(), 1));
            tags.push(("mineral".into(), 1));
        } else if lower.contains("meat") {
            tags.push(("meat".into(), 1));
            tags.push(("eatable".into(), 1));
        } else if lower.contains("bone") {
            tags.push(("bone".into(), 1));
        } else if lower.contains("leather") || lower.contains("hide") {
            tags.push(("leather".into(), 1));
        }
        tags
    }

    pub fn is_consumable(&self, prototype_id: &str) -> bool {
        if let Some(m) = self.prototypes.get(prototype_id) {
            return m.eatable;
        }
        let lower = prototype_id.to_ascii_lowercase();
        lower.contains("berry")
            || lower.contains("fruit")
            || lower.contains("meat")
            || lower.contains("food")
            || lower.contains("water")
            || lower.contains("roast")
            || lower.contains("skewer")
            || lower.contains("medicine")
            || lower.contains("bandage")
    }

    pub fn is_item_equippable_in_slot(&self, prototype_id: &str, slot_name: &str) -> bool {
        let lower = prototype_id.to_ascii_lowercase();
        let slot = slot_name.to_ascii_lowercase();
        let is_weapon_slot =
            matches!(slot.as_str(), "weapon" | "main" | "sub" | "both" | "tool" | "shield");
        let is_clothing_slot = matches!(
            slot.as_str(),
            "body"
                | "clothes"
                | "hoody"
                | "head"
                | "helmet"
                | "hat"
                | "shoes"
                | "boots"
                | "gloves"
                | "hand"
        );
        let is_bag_slot = slot == "bag";

        let perfs = self.item_performances(prototype_id, 1);
        for p in &perfs {
            if let Some(req_slot) = p.strs.get("slot") {
                return req_slot.eq_ignore_ascii_case(&slot)
                    || (req_slot.eq_ignore_ascii_case("main")
                        && matches!(slot.as_str(), "weapon" | "main" | "sub"));
            }
            if matches!(p.id.as_str(), "weapon" | "tool") {
                return is_weapon_slot;
            }
            if p.id == "armor" {
                return is_clothing_slot;
            }
            if p.id == "bag" {
                return is_bag_slot;
            }
        }

        let looks_weapon = lower.starts_with("tool_")
            || lower.starts_with("weapon_")
            || lower.starts_with("blade_")
            || lower.contains("knife")
            || lower.contains("axe")
            || lower.contains("pickaxe")
            || lower.contains("hammer")
            || lower.contains("bow")
            || lower.contains("spear")
            || lower.contains("sword");
        let looks_clothing = lower.starts_with("clothes_")
            || lower.starts_with("armor_")
            || lower.contains("hood")
            || lower.contains("helmet")
            || lower.contains("hat")
            || lower.contains("boots")
            || lower.contains("shoes")
            || lower.contains("gloves")
            || lower.contains("suit")
            || lower.contains("outfit");
        let looks_bag =
            lower.starts_with("bag_") || lower.contains("backpack") || lower.contains("pouch");

        if let Some(m) = self.prototypes.get(prototype_id) {
            let cat = m.category.to_ascii_lowercase();
            let has_eq_tag = m.tags.iter().any(|(t, _)| {
                matches!(
                    t.as_str(),
                    "equippable" | "weapon" | "armor" | "tool" | "bag" | "clothes" | "accessory"
                )
            });
            if !has_eq_tag
                && !matches!(
                    cat.as_str(),
                    "weapon" | "tool" | "clothing" | "armor" | "bag" | "accessory" | "equipment"
                )
                && !looks_weapon
                && !looks_clothing
                && !looks_bag
            {
                return false;
            }
            if is_clothing_slot && (cat == "weapon" || cat == "tool" || looks_weapon) {
                return false;
            }
            if is_weapon_slot && (cat == "clothing" || cat == "armor" || looks_clothing) {
                return false;
            }
            return true;
        }

        if is_weapon_slot {
            looks_weapon
        } else if is_clothing_slot {
            looks_clothing
        } else if is_bag_slot {
            looks_bag
        } else {
            looks_weapon || looks_clothing || looks_bag
        }
    }

    pub fn weapon_framework_for(&self, prototype_id: &str, level: i32) -> Option<String> {
        for p in self.item_performances(prototype_id, level) {
            if let Some(wf) = p.strs.get("weapon_framework") {
                if !wf.is_empty() {
                    return Some(wf.clone());
                }
            }
        }
        let lower = prototype_id.to_ascii_lowercase();
        if lower.contains("twohand") || lower.contains("spear") || lower.contains("lance") {
            Some("twohand".into())
        } else if lower.contains("bow") {
            Some("bow".into())
        } else if lower.contains("sling") {
            Some("sling".into())
        } else if lower.contains("knife")
            || lower.contains("axe")
            || lower.contains("sword")
            || lower.contains("hammer")
            || lower.contains("pickaxe")
            || lower.contains("weapon")
        {
            Some("onehand".into())
        } else {
            None
        }
    }

    pub fn weapon_attack_bonus(&self, prototype_id: &str, level: i32) -> i32 {
        for p in self.item_performances(prototype_id, level) {
            for key in ["attack", "damage", "melee_attack", "ranged_attack"] {
                if let Some(&val) = p.nums.get(key) {
                    if val > 0.0 {
                        return (val.round() as i32).max(5);
                    }
                }
            }
        }
        15 + ((level.clamp(1, 60) - 1) / 2)
    }

    /// Returns `(life_delta, energy_delta, fatigue_delta)` for consuming `prototype_id` at `level`.
    pub fn consumable_effects(&self, prototype_id: &str, level: i32) -> (f32, f32, f32) {
        let lv = level.clamp(1, 60) as f32;
        let lower = prototype_id.to_ascii_lowercase();
        let tags: Vec<String> = self
            .prototype_tags(prototype_id)
            .into_iter()
            .map(|(t, _)| t)
            .collect();
        let cat = self
            .prototypes
            .get(prototype_id)
            .map(|m| m.category.to_ascii_lowercase())
            .unwrap_or_default();

        if cat.contains("medicine")
            || lower.contains("medicine")
            || lower.contains("bandage")
            || lower.contains("potion")
            || lower.contains("salve")
            || tags.iter().any(|t| t.contains("medicine") || t == "bandage")
        {
            return (25.0 + lv * 0.8, 5.0, -5.0);
        }
        if lower.contains("water")
            || lower.contains("juice")
            || lower.contains("tea")
            || lower.contains("drink")
            || lower.contains("soup")
            || tags.iter().any(|t| t == "drink" || t == "beverage")
        {
            return (4.0, 12.0 + lv * 0.3, -(18.0 + lv * 0.3));
        }
        if cat.contains("cook")
            || lower.contains("roast")
            || lower.contains("skewer")
            || lower.contains("steak")
            || lower.contains("stew")
            || lower.contains("cooked")
            || tags.iter().any(|t| t == "cooked")
        {
            return (15.0 + lv * 0.4, 30.0 + lv * 0.8, -(12.0 + lv * 0.2));
        }
        (6.0, 22.0 + lv * 0.3, -4.0)
    }

    pub fn resolve_recipe(&self, recipe_id: &str) -> Option<&RecipeSpec> {
        self.recipes.get(recipe_id)
    }

    pub fn resolve_blueprint(&self, blueprint_id: &str) -> ArtifactSpec {
        if let Some(&et) = self.blueprint_to_type.get(blueprint_id) {
            if let Some(spec) = self.artifacts_by_type.get(&et) {
                return spec.clone();
            }
        }
        let lower = blueprint_id.to_ascii_lowercase();
        let can_rest = lower.contains("tent")
            || lower.contains("bed")
            || lower.contains("chair")
            || lower.contains("bonfire")
            || lower.contains("shelter");
        ArtifactSpec {
            entity_type: 6151,
            blueprint_id: blueprint_id.to_string(),
            display_name: blueprint_id.to_string(),
            size_x: 1,
            size_y: 1,
            height: 1,
            can_rest,
            is_craft: true,
            is_home: can_rest,
            is_port: lower == "dock",
            is_burnable: lower.contains("bonfire") || lower.contains("kiln") || lower.contains("furnace"),
            is_gate: lower.contains("gate"),
            is_warehouse: lower.contains("box") || lower.contains("basket") || lower.contains("warehouse"),
            is_washable: lower.contains("well"),
            is_warphole: lower.contains("warphole"),
            default_parts: {
                let mut m = BTreeMap::new();
                m.insert("common".to_string(), blueprint_id.to_string());
                m
            },
            workbench_tags: fallback_workbench_tags(blueprint_id),
        }
    }

    pub fn resolve_artifact_type(&self, entity_type: u16) -> Option<&ArtifactSpec> {
        self.artifacts_by_type.get(&entity_type)
    }

    pub fn artifact_default_parts(&self, entity_type: u16, blueprint_id: &str) -> BTreeMap<String, String> {
        if let Some(spec) = self.artifacts_by_type.get(&entity_type) {
            if !spec.default_parts.is_empty() {
                return spec.default_parts.clone();
            }
        }
        let mut m = BTreeMap::new();
        if !blueprint_id.is_empty() {
            m.insert("common".to_string(), blueprint_id.to_string());
        }
        m
    }

    pub fn artifact_workbench_tags(&self, entity_type: u16, blueprint_id: &str) -> Vec<(String, i32)> {
        if let Some(spec) = self.artifacts_by_type.get(&entity_type) {
            if !spec.workbench_tags.is_empty() {
                return spec.workbench_tags.clone();
            }
        }
        fallback_workbench_tags(blueprint_id)
    }

    pub fn item_performances(&self, prototype_id: &str, level: i32) -> Vec<durango_wire::PerformanceMsg> {
        let lv = level.max(1);
        let mut out = Vec::new();
        if let Some(list) = self.performances_by_proto.get(prototype_id) {
            for tpl in list {
                if lv < tpl.min_level || lv > tpl.max_level {
                    continue;
                }
                let mut nums = BTreeMap::new();
                for (k, expr) in &tpl.num_exprs {
                    if let Some(val) = eval_perf_formula(expr, lv) {
                        nums.insert(k.clone(), val);
                    }
                }
                out.push(durango_wire::PerformanceMsg {
                    id: tpl.id.clone(),
                    name: tpl.name.clone(),
                    icon: tpl.icon.clone(),
                    nums,
                    strs: tpl.strs.clone(),
                });
            }
        } else if prototype_id.starts_with("blade_")
            || prototype_id.starts_with("knife_")
            || prototype_id.starts_with("axe_")
            || prototype_id.starts_with("sword_")
            || prototype_id.contains("knife")
        {
            // Headless unit-test fallback when performance.json is not present
            let mut nums = BTreeMap::new();
            nums.insert("attack".into(), 14.0 + lv as f32 * 1.2);
            nums.insert("accuracy".into(), 2.0 + lv as f32 * 2.0);
            nums.insert("attack_cooltime".into(), 2.0);
            let mut strs = BTreeMap::new();
            strs.insert("slot".into(), "main".into());
            strs.insert("weapon_framework".into(), "onehand".into());
            strs.insert("attack_type".into(), "sword".into());
            out.push(durango_wire::PerformanceMsg {
                id: "weapon".into(),
                name: "Weapon".into(),
                icon: String::new(),
                nums,
                strs,
            });
        }
        out
    }

    pub fn resolve_animal(&self, entity_type: u16) -> AnimalSpec {
        self.animals_by_type
            .get(&entity_type)
            .cloned()
            .unwrap_or_else(|| AnimalSpec {
                entity_type,
                raw_id: format!("dinosaur_{entity_type}"),
                name: format!("dinosaur_{entity_type}"),
                max_hp: 60.0,
                base_scale: 1.0,
                size_level: 2,
                difficulty: 1.0,
                min_island_level: 1,
                min_level: 1,
                max_level: 10,
                is_aggressive: false,
                wild_eligible: true,
                attack_cooltime: 2.2,
                stand_motion: "Para_Stand".into(),
                move_motion: "Para_Walk".into(),
                dead_motion: "Para_Die".into(),
                battle_stand_motion: "Para_BattleStand".into(),
                attack_motion: "Para_Attack_Stamp".into(),
                attacks: vec!["Para_Attack_Stamp".into()],
                attack_damage: 14,
            })
    }

    /// Returns a curated, island-tier-appropriate pool of valid wildlife species.
    /// - Safe / Tamed starter islands (`is_safe_tamed_island == true`): ONLY gentle starter grassland
    ///   fauna (`size_level <= 2`, `difficulty <= 1.2`, non-aggressive/timid) — ZERO Brachiosaurus,
    ///   ZERO Dodophysis, ZERO aggressive apex predators!
    /// - Wild / Unstable islands: strictly filtered by `min_island_level <= island_level` so giant
    ///   sauropods & apex carnivores only appear on high-tier islands (Lv 45+).
    pub fn island_wildlife_pool(
        &self,
        island_level: i32,
        is_safe_tamed_island: bool,
    ) -> (Vec<AnimalSpec>, Vec<AnimalSpec>) {
        let mut peaceful = Vec::new();
        let mut aggressive = Vec::new();
        for spec in self.animals_by_type.values() {
            if !spec.wild_eligible {
                continue;
            }
            if spec.move_motion == spec.stand_motion || spec.dead_motion == spec.stand_motion {
                continue;
            }
            if is_safe_tamed_island {
                if spec.is_aggressive
                    || spec.min_island_level > 10
                    || spec.size_level > 2
                    || spec.difficulty > 1.2
                {
                    continue;
                }
                peaceful.push(spec.clone());
                continue;
            }
            if spec.min_island_level > island_level || spec.min_level > island_level + 2 {
                continue;
            }
            if spec.is_aggressive {
                aggressive.push(spec.clone());
            } else {
                peaceful.push(spec.clone());
            }
        }
        peaceful.sort_by_key(|s| s.entity_type);
        aggressive.sort_by_key(|s| s.entity_type);
        (peaceful, aggressive)
    }

    pub fn resolve_battle_action(&self, action_id: &str) -> Option<&BattleActionSpec> {
        self.battle_actions.get(action_id)
    }

    /// Resolves the exact Durango `Actions (315)` list for the player's equipped item tags
    /// using `tag_allow_actions.json` (falling back to `bare_hands` when no weapon tag matches).
    pub fn battle_actions_for_tags(&self, equipped_tags: &[String]) -> Vec<BattleActionSpec> {
        let mut seen = std::collections::HashSet::new();
        let mut out = Vec::new();
        for tag in equipped_tags {
            if let Some(ids) = self.tag_allow_actions.get(tag) {
                for aid in ids {
                    if seen.insert(aid.clone()) {
                        if let Some(spec) = self.battle_actions.get(aid) {
                            out.push(spec.clone());
                        }
                    }
                }
            }
        }
        if out.is_empty() {
            if let Some(ids) = self.tag_allow_actions.get("bare_hands") {
                for aid in ids {
                    if seen.insert(aid.clone()) {
                        if let Some(spec) = self.battle_actions.get(aid) {
                            out.push(spec.clone());
                        }
                    }
                }
            }
        }
        out
    }

    pub fn butcher_generators(&self, level: i32) -> Vec<GeneratorSpec> {
        let protos = [
            ("meat", "Raw Meat", "icon_nat_meat"),
            ("leather", "Hide", "icon_nat_leather"),
            ("bone_leg", "Leg Bone", "icon_nat_bone"),
        ];
        protos
            .iter()
            .enumerate()
            .map(|(idx, &(proto, fallback_name, fallback_icon))| {
                let (name, icon) = self
                    .prototypes
                    .get(proto)
                    .map(|m| (m.name.clone(), m.icon.clone()))
                    .unwrap_or_else(|| (fallback_name.to_string(), fallback_icon.to_string()));
                GeneratorSpec {
                    id: proto.to_string(),
                    prototype_id: proto.to_string(),
                    name,
                    icon,
                    level: level.max(1),
                    amount: if idx == 0 { 3 } else { 2 },
                    effort: 10.0,
                    duration: 1.5,
                    tool_requirements: default_tool_requirements(proto),
                }
            })
            .collect()
    }
}

fn fallback_workbench_tags(blueprint_id: &str) -> Vec<(String, i32)> {
    let lower = blueprint_id.to_ascii_lowercase();
    if lower.contains("bonfire") || lower.contains("stove") {
        vec![("fireplace".into(), 1)]
    } else if lower.contains("kiln") {
        vec![("kiln".into(), 1)]
    } else if lower.contains("furnace") {
        vec![("furnace".into(), 1)]
    } else if lower.contains("kitchen") || lower.contains("cooking") {
        vec![("table_cooking".into(), 1), ("fireplace".into(), 1)]
    } else if lower.contains("sewing") || lower.contains("loom") || lower.contains("clothes") {
        vec![("table_clothes".into(), 1)]
    } else if lower.contains("workbench") || lower.contains("table") {
        vec![("table".into(), 1)]
    } else if lower.contains("drying") {
        vec![("drying_rack".into(), 1)]
    } else if lower.contains("tanning") {
        vec![("tanning_stand".into(), 1)]
    } else {
        Vec::new()
    }
}

fn default_tool_requirements(proto: &str) -> BTreeMap<String, i32> {
    // Always include "bare_hands": 1 so Unity's GatheringData.FindBestTool sets
    // BestPerformance > 0 — required for full-brightness radial icons, predict timers,
    // and ReservationQueue stacked activity queuing (e.g. 3 berries + 2 leaves).
    let mut reqs = BTreeMap::new();
    reqs.insert("bare_hands".to_string(), 1);
    let lower = proto.to_ascii_lowercase();
    if lower.contains("wood") || lower.contains("log") || lower.contains("bough") {
        reqs.insert("axe".to_string(), 1);
        reqs.insert("knife".to_string(), 1);
    } else if lower.contains("ore") || lower.contains("stone") || lower.contains("rock") {
        reqs.insert("pickaxe".to_string(), 1);
        reqs.insert("hammer".to_string(), 1);
    } else if lower.contains("meat") || lower.contains("leather") || lower.contains("bone") {
        reqs.insert("knife".to_string(), 1);
    } else {
        reqs.insert("sickle".to_string(), 1);
        reqs.insert("knife".to_string(), 1);
    }
    reqs
}

fn extract_gettext_key(v: &Value) -> Option<String> {
    v.as_object()
        .and_then(|m| m.keys().next())
        .filter(|s| !s.is_empty())
        .cloned()
}

/// Cleanroom GNU gettext `.mo` parser for official English `locales/en_US/LC_MESSAGES/messages.mo`.
fn load_english_mo(assets_dir: Option<&Path>) -> HashMap<String, String> {
    let mut candidates = Vec::new();
    if let Some(dir) = assets_dir {
        if let Some(parent) = dir.parent() {
            candidates.push(parent.join("locales/en_US/LC_MESSAGES/messages.mo"));
            candidates.push(parent.join("locales/en/LC_MESSAGES/messages.mo"));
        }
    }
    candidates.push(Path::new(".work/data/locales/en_US/LC_MESSAGES/messages.mo").to_path_buf());
    candidates.push(
        Path::new("_backup_2026-11-19/workspace-artifacts/build/server/data/locales/en_US/LC_MESSAGES/messages.mo")
            .to_path_buf(),
    );

    for path in candidates {
        if let Ok(bytes) = std::fs::read(&path) {
            let map = parse_mo_bytes(&bytes);
            if !map.is_empty() {
                tracing::info!(entries = map.len(), path = %path.display(), "loaded English localization (.mo)");
                return map;
            }
        }
    }
    HashMap::new()
}

fn parse_mo_bytes(bytes: &[u8]) -> HashMap<String, String> {
    let mut out = HashMap::new();
    if bytes.len() < 28 {
        return out;
    }
    let magic = u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]);
    let little = match magic {
        0x9504_12de => true,
        0xde12_0495 => false,
        _ => return out,
    };
    let read_u32 = |off: usize| -> u32 {
        let s = [bytes[off], bytes[off + 1], bytes[off + 2], bytes[off + 3]];
        if little {
            u32::from_le_bytes(s)
        } else {
            u32::from_be_bytes(s)
        }
    };
    let count = read_u32(8) as usize;
    let orig_tab = read_u32(12) as usize;
    let trans_tab = read_u32(16) as usize;
    let Some(tab_bytes) = count.checked_mul(8) else {
        return out;
    };
    let Some(orig_end) = orig_tab.checked_add(tab_bytes) else {
        return out;
    };
    let Some(trans_end) = trans_tab.checked_add(tab_bytes) else {
        return out;
    };
    if orig_end > bytes.len() || trans_end > bytes.len() {
        return out;
    }
    out.reserve(count.min(4096));
    for i in 0..count {
        let entry_off = i * 8;
        let olen = read_u32(orig_tab + entry_off) as usize;
        let ooff = read_u32(orig_tab + entry_off + 4) as usize;
        let tlen = read_u32(trans_tab + entry_off) as usize;
        let toff = read_u32(trans_tab + entry_off + 4) as usize;
        let (Some(o_end), Some(t_end)) = (ooff.checked_add(olen), toff.checked_add(tlen)) else {
            continue;
        };
        if olen == 0 || tlen == 0 || o_end > bytes.len() || t_end > bytes.len() {
            continue;
        }
        if let (Ok(k), Ok(v)) = (
            std::str::from_utf8(&bytes[ooff..ooff + olen]),
            std::str::from_utf8(&bytes[toff..toff + tlen]),
        ) {
            if !k.is_empty() && !v.is_empty() {
                let normalized = if v.contains("\\n") {
                    v.replace("\\\\n", "\n").replace("\\n", "\n")
                } else {
                    v.to_string()
                };
                out.insert(k.to_string(), normalized);
            }
        }
    }
    out
}

fn translate_name(mo: &HashMap<String, String>, key: Option<&str>, fallback_id: &str) -> String {
    if let Some(k) = key {
        if let Some(en) = mo.get(k) {
            let clean = en.trim();
            if !clean.is_empty() {
                return clean.to_string();
            }
        }
        // Only pass raw key through if it is ASCII-renderable in Linux/Wine UI fonts
        if k.is_ascii() && !k.contains('_') && !k.trim().is_empty() {
            return k.trim().to_string();
        }
    }
    humanize_id(fallback_id)
}

fn humanize_id(id: &str) -> String {
    match id {
        "wood_bough" => return "Branch".into(),
        "wood_log" => return "Log".into(),
        "leaf_small" => return "Leaf".into(),
        "fruit_berry" => return "Wild Berry".into(),
        "stem_fiber" => return "Reed Stem".into(),
        "ore_iron" => return "Iron Ore".into(),
        "ore_copper" => return "Copper Ore".into(),
        "stone" => return "Stone".into(),
        "meat" => return "Raw Meat".into(),
        "leather" => return "Hide".into(),
        "bone" => return "Bone".into(),
        _ => {}
    }
    let parts: Vec<String> = id
        .split('_')
        .filter(|s| !s.is_empty())
        .map(|w| {
            let mut cs = w.chars();
            match cs.next() {
                None => String::new(),
                Some(first) => first.to_uppercase().collect::<String>() + cs.as_str(),
            }
        })
        .collect();
    if parts.is_empty() {
        "Resource".into()
    } else {
        parts.join(" ")
    }
}

pub fn default_icon_for(proto: &str) -> &'static str {
    if proto.contains("knife") || proto.contains("axe") || proto.contains("tool") {
        "blade_stone"
    } else if proto.contains("stone") || proto.contains("ore") {
        "icon_nat_mine_stone"
    } else if proto == "wildberry" {
        "icon_nat_fruit_wildberry"
    } else if proto.contains("berry") || proto.contains("fruit") {
        "tag_material_fruit"
    } else if proto.contains("leaf") || proto.contains("stem") {
        "icon_nat_leaf"
    } else if proto.contains("bone") {
        "icon_nat_bone"
    } else if proto.contains("meat") {
        "icon_nat_meat"
    } else if proto.contains("leather") || proto.contains("hide") {
        "icon_nat_leather"
    } else if proto.contains("branch") || proto.contains("bough") {
        "icon_nat_wood_branch"
    } else {
        "icon_nat_wood_log"
    }
}

fn parse_level_range_key(key: &str) -> (i32, i32) {
    let trimmed = key.trim().trim_start_matches('[').trim_end_matches(']');
    if let Some((a, b)) = trimmed.split_once(',') {
        let min_lv = a.trim().parse::<i32>().unwrap_or(1);
        let max_lv = b.trim().parse::<i32>().unwrap_or(60);
        return (min_lv, max_lv);
    }
    (1, 60)
}

fn looks_like_numeric_formula(s: &str) -> bool {
    if s.is_empty() {
        return false;
    }
    let cleaned = s
        .replace("range_lookup", "")
        .replace("level", "1")
        .replace("int", "")
        .replace("min", "")
        .replace("max", "");
    cleaned.chars().all(|c| {
        c.is_ascii_digit()
            || matches!(
                c,
                '.' | '+' | '-' | '*' | '/' | '(' | ')' | '[' | ']' | ',' | ' '
            )
    }) && (cleaned.chars().any(|c| c.is_ascii_digit()) || s.contains("level"))
}

fn eval_perf_formula(expr: &str, level: i32) -> Option<f32> {
    let s = expr.trim();
    if s.is_empty() {
        return None;
    }
    if let Ok(v) = s.parse::<f32>() {
        return Some(v);
    }
    if s == "level" {
        return Some(level as f32);
    }
    let replaced = s.replace("level", &format!("{}", level as f32));
    let bytes = replaced.as_bytes();
    let mut pos = 0usize;

    fn skip_ws(b: &[u8], p: &mut usize) {
        while *p < b.len() && b[*p].is_ascii_whitespace() {
            *p += 1;
        }
    }
    fn parse_expr(b: &[u8], p: &mut usize) -> Option<f32> {
        let mut lhs = parse_term(b, p)?;
        loop {
            skip_ws(b, p);
            if *p >= b.len() {
                break;
            }
            match b[*p] {
                b'+' => {
                    *p += 1;
                    lhs += parse_term(b, p)?;
                }
                b'-' => {
                    *p += 1;
                    lhs -= parse_term(b, p)?;
                }
                _ => break,
            }
        }
        Some(lhs)
    }
    fn parse_term(b: &[u8], p: &mut usize) -> Option<f32> {
        let mut lhs = parse_power(b, p)?;
        loop {
            skip_ws(b, p);
            if *p >= b.len() {
                break;
            }
            match b[*p] {
                b'*' if !b[*p..].starts_with(b"**") => {
                    *p += 1;
                    lhs *= parse_power(b, p)?;
                }
                b'/' => {
                    *p += 1;
                    let r = parse_power(b, p)?;
                    if r.abs() < 1e-6 {
                        return Some(0.0);
                    }
                    lhs /= r;
                }
                _ => break,
            }
        }
        Some(lhs)
    }
    fn parse_power(b: &[u8], p: &mut usize) -> Option<f32> {
        let lhs = parse_factor(b, p)?;
        skip_ws(b, p);
        if b[*p..].starts_with(b"**") {
            *p += 2;
            let rhs = parse_power(b, p)?;
            let out = lhs.powf(rhs);
            return if out.is_finite() { Some(out) } else { None };
        }
        Some(lhs)
    }
    fn parse_range_lookup(b: &[u8], p: &mut usize) -> Option<f32> {
        skip_ws(b, p);
        if *p >= b.len() || b[*p] != b'[' {
            return None;
        }
        *p += 1;
        let mut pairs: Vec<(f32, f32)> = Vec::new();
        loop {
            skip_ws(b, p);
            if *p >= b.len() {
                return None;
            }
            if b[*p] == b']' {
                *p += 1;
                break;
            }
            if b[*p] == b',' {
                *p += 1;
                continue;
            }
            if b[*p] != b'(' {
                return None;
            }
            *p += 1;
            let thresh = parse_expr(b, p)?;
            skip_ws(b, p);
            if *p >= b.len() || b[*p] != b',' {
                return None;
            }
            *p += 1;
            let val = parse_expr(b, p)?;
            skip_ws(b, p);
            if *p < b.len() && b[*p] == b')' {
                *p += 1;
            }
            pairs.push((thresh, val));
        }
        skip_ws(b, p);
        if *p >= b.len() || b[*p] != b',' {
            return None;
        }
        *p += 1;
        let key = parse_expr(b, p)?;
        skip_ws(b, p);
        if *p < b.len() && b[*p] == b')' {
            *p += 1;
        }
        let mut best = 0.0f32;
        for (thresh, val) in pairs {
            if key >= thresh - 1e-5 {
                best = val;
            }
        }
        Some(best)
    }
    fn parse_factor(b: &[u8], p: &mut usize) -> Option<f32> {
        skip_ws(b, p);
        if *p >= b.len() {
            return None;
        }
        if b[*p] == b'-' {
            *p += 1;
            return Some(-parse_factor(b, p)?);
        }
        if b[*p] == b'+' {
            *p += 1;
            return parse_factor(b, p);
        }
        if b[*p] == b'(' {
            *p += 1;
            let v = parse_expr(b, p)?;
            skip_ws(b, p);
            if *p < b.len() && b[*p] == b')' {
                *p += 1;
            }
            return Some(v);
        }
        if b[*p].is_ascii_alphabetic() || b[*p] == b'_' {
            let start = *p;
            while *p < b.len() && (b[*p].is_ascii_alphanumeric() || b[*p] == b'_') {
                *p += 1;
            }
            let ident = std::str::from_utf8(&b[start..*p]).ok()?;
            skip_ws(b, p);
            if *p >= b.len() || b[*p] != b'(' {
                return None;
            }
            *p += 1;
            match ident {
                "range_lookup" => return parse_range_lookup(b, p),
                "int" => {
                    let v = parse_expr(b, p)?;
                    skip_ws(b, p);
                    if *p < b.len() && b[*p] == b')' {
                        *p += 1;
                    }
                    return Some((v as i64) as f32);
                }
                "min" | "max" => {
                    let mut best = parse_expr(b, p)?;
                    loop {
                        skip_ws(b, p);
                        if *p >= b.len() {
                            break;
                        }
                        if b[*p] == b',' {
                            *p += 1;
                            let next = parse_expr(b, p)?;
                            best = if ident == "min" {
                                best.min(next)
                            } else {
                                best.max(next)
                            };
                        } else {
                            if b[*p] == b')' {
                                *p += 1;
                            }
                            break;
                        }
                    }
                    return Some(best);
                }
                _ => return None,
            }
        }
        let start = *p;
        while *p < b.len() && (b[*p].is_ascii_digit() || b[*p] == b'.') {
            *p += 1;
        }
        if *p == start {
            return None;
        }
        std::str::from_utf8(&b[start..*p]).ok()?.parse::<f32>().ok()
    }
    let out = parse_expr(bytes, &mut pos)?;
    skip_ws(bytes, &mut pos);
    if pos == bytes.len() && out.is_finite() {
        Some(out)
    } else {
        None
    }
}

#[cfg(test)]
mod formula_tests {
    use super::*;

    #[test]
    fn eval_perf_formula_supports_range_lookup_int_min_and_power() {
        let rl = "range_lookup([(50, 3),(55, 4),(60, 5)], level)";
        assert_eq!(eval_perf_formula(rl, 10), Some(0.0));
        assert_eq!(eval_perf_formula(rl, 50), Some(3.0));
        assert_eq!(eval_perf_formula(rl, 57), Some(4.0));
        assert_eq!(eval_perf_formula(rl, 60), Some(5.0));

        assert_eq!(
            eval_perf_formula("min(0.18, 0.015 + 0.01 * int(level/10))", 25),
            Some(0.035)
        );
        assert_eq!(
            eval_perf_formula("min(0.18, 0.015 + 0.01 * int(level/10))", 200),
            Some(0.18)
        );
        let groggy = eval_perf_formula("int((135 + (level - 1) * 9 + ((0.5 * level) ** 1.8)) * 2)", 10).unwrap();
        assert!((groggy - 468.0).abs() < 1.0, "groggy={groggy}");
        assert!(looks_like_numeric_formula(rl));
    }

    #[test]
    fn parse_mo_bytes_decodes_literal_newlines() {
        let key = b"k";
        let val = br"line1\nline2";
        let header_len = 28u32;
        let orig_tab_off = header_len;
        let trans_tab_off = orig_tab_off + 8;
        let key_off = trans_tab_off + 8;
        let val_off = key_off + key.len() as u32 + 1;

        let mut mo = Vec::new();
        for word in [
            0x9504_12deu32,
            0,
            1,
            orig_tab_off,
            trans_tab_off,
            0,
            0,
            key.len() as u32,
            key_off,
            val.len() as u32,
            val_off,
        ] {
            mo.extend_from_slice(&word.to_le_bytes());
        }
        mo.extend_from_slice(key);
        mo.push(0);
        mo.extend_from_slice(val);
        mo.push(0);

        let parsed = parse_mo_bytes(&mo);
        assert_eq!(parsed.get("k").map(String::as_str), Some("line1\nline2"));
    }
}
