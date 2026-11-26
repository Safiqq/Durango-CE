use std::collections::{BTreeMap, HashMap, HashSet, VecDeque};
use std::net::SocketAddr;
use std::sync::atomic::{AtomicU32, AtomicU64, Ordering};
use std::sync::Arc;
use std::time::Duration;

use durango_wire::{
    encode_frame, pack_empty_factions, pack_empty_mission_infos, pack_empty_musics,
    pack_empty_nomad_info, pack_empty_quest_categories, pack_empty_resistance_exp_caps,
    pack_empty_returner_info, pack_empty_single_array, pack_empty_single_map, pack_empty_social,
    pack_empty_struct, pack_empty_support_requests, pack_empty_warp_costs, try_decode_frame,
    AbortMsg, ActionStatusMsg, ActionsMsg, AppearAnimalMsg, AppearArtifactMsg, AppearPlayerMsg,
    ArchipelagoMsg, ArchipelagoRouteMsg, ArtifactBlueprintsMsg, ArtifactBuiltMsg,
    ArtifactCompletedMsg, ArtifactMaterialsMsg, AttachableAccessoriesMsg, Auth,
    AvailableEmotionsMsg, BattleBegunMsg, BattleEndedMsg, BuildArtifactMsg, BuildEstimationMsg,
    BuyProductMsg, CancelCraftingMsg, CheatFlagsMsg, ChunkMsg, Clock, CollectMsg, CollectedMsg,
    CollectibleChangedMsg, CollectibleMsg, CombatInteractionMsg, CompleteArtifactMsg,
    ContainerItemsOpMsg, CraftEstimationInfoMsg, CraftEstimationMsg, CraftMsg, CraftedMsg,
    DamageMsg, DamagedMsg, DeclareEstateMsg, DefoggedChunks, DestructArtifactMsg, DestructingMsg,
    DisappearEntity, DisappearEntityOnTileMsg, DiscoveryInfoMsg, DumpItemsMsg, EmigratedMsg,
    EnterBattleMsg, EntityDiedMsg, EntityRevivedMsg, EntityTileMsg, EquipMsg, EquipmentsMsg,
    EstateCellOpMsg, EstateGridsMsg, EstateLicenseMsg, EstateLicensesMsg, EstimateBuildMsg,
    EstimateCraftMsg, ExpGainedMsg, ExploredPoisMsg, ExtendEstateActivationMsg,
    FatigueVelocitiesMsg, GaugeMsg, GeneratorMsg, GetClock, GetCollectibleMsg, GetInventoryMsg,
    GetQuestsMsg, GetRegionMapInfoMsg, InventoryInfosMsg, InventoryMsg, InventoryOrderMsg,
    InventoryUpdatedMsg, ItemMsg, ItemTagMsg, LastSearchedTimeMsg, LearnSkillMsg, LeaveClanMsg,
    Location, LockOrUnlockItemsMsg, MailIdsMsg, MailMsg, MailsMsg, MakeClanMsg, MemberMsg,
    MountAirBalloonMsg, MoveItemsInWarehouseMsg, MoveMsg, Movement, NearestPoiMsg, OccupiedMsg,
    OccupyArtifactSiteMsg,
    PersonalRegionInfoMsg, PersonalRegionMsg, PetsInfoMsg, PioneerGradeInfoMsg, PlayerDisplayMsg,
    PoiCountMsg, PointEntityTileMsg, PointOfInterestMsg, PointsMsg, ProductMsg, ProductsMsg,
    PutMaterialsIntoArtifactMsg, QuestsMsg, RecipesMsg, RecommendArchipelagoMsg,
    RecommendRegionMsg, RecommendedStableRegionsMsg, RegionMapInfoMsg, RegionMsg, RegionRole,
    RegionTileMsg, RegisterMultipleProductsMsg, RegisterProductMsg, RenameWarehouseSectionMsg,
    RepairArtifactMsg, RepairImmediateMsg, RepairItemMsg, RequestNearestPoiMsg, ResearchSkillCategoryMsg, RestOnMsg, ReturnToEstateMsg,
    ReviveImmediatelyInfoMsg, ReviveImmediatelyMsg, ReviveMsg, RewardedLevelUpMsg, RoutesMsg,
    RoutesOfArchipelagoMsg, RunDownMsg, RunUpMsg, SailingBackCostMsg, SayInExclusiveChannelMsg,
    SearchProductsMsg, SearchedPoisMsg, SelectBattleTargetMsg, SendMailMsg, SetBaseMoveSpeedMsg,
    SetChunk, SetEstateLicenseMsg, SetLikeMsg, SetPersonalRegionAdmissionMsg, SetStorageItemMsg,
    SingleStringMsg, SkillBundleMsg, SkillCategoryMsg, SkillsMsg, StatisticsMsg, StatusEffectMsg,
    StatusEffectsMsg, SurvivalMsg, TeleportedMsg, TimerMsg, ToggleStatusEffectMsg, TouchMsg,
    TouchedMsg, TravelByRegionInArchipelagoMsg, TravelByRegionMsg, TravelToStableRegionMsg,
    UntrainSkillMsg, UseBattleActionMsg, UseItemMsg, WalletUpdatedMsg, WarehouseItemsOpMsg,
    WarehouseMsg, WarehouseSectionInfoMsg, WarehouseSectionOpMsg, WarpMsg, Welcome, WorkbenchMsg,
    ANIMAL_STATUS_BATTLE, ANIMAL_STATUS_DEAD, ANIMAL_STATUS_PEACE, ATTACK_TYPE_BARE_HANDS,
    ATTACK_TYPE_DAGGER, ATTACK_TYPE_LARGE_BODY, ATTACK_TYPE_SMALL_BODY, BOARDING_ON_AIR_BALLOON,
    BOARDING_ON_NONE, BODY_PART_BODY, BUILDING_STATE_BUILT, BUILDING_STATE_COMPLETED,
    BUILDING_STATE_OCCUPIED, DAMAGE_DIR_FRONT,
    DAMAGE_EFFECT_BLOW, DAMAGE_EFFECT_KNOCK_BACK, DAMAGE_RESULT_EVADE, DAMAGE_RESULT_HIT,
    EQUIP_SLOT_1, INTERACTION_ATTACK, INTERACTION_BUILD_ARTIFACT, INTERACTION_CLOSE_GATE,
    INTERACTION_COLLECT, INTERACTION_COMPLETE_ARTIFACT, INTERACTION_CRAFT,
    INTERACTION_DESTRUCT_ARTIFACT, INTERACTION_EXTINGUISH, INTERACTION_FIRE,
    INTERACTION_INVENTORY, INTERACTION_OPEN_GATE, INTERACTION_REPAIR_ARTIFACT, INTERACTION_REST,
    INTERACTION_SAILING_RANDOM_PERSONAL, INTERACTION_SAILING_ROUTES, INTERACTION_SAILING_WITHDRAW,
    INTERACTION_SET_AS_HOME, INTERACTION_USE_WAREHOUSE, INTERACTION_WARP, INTERACTION_WASH,
    MOTION_OPT_LOOPING, MOTION_OPT_LOOP_ALIGN, MOTION_OPT_NORMAL, OWNER_TYPE_CLAN_ESTATE,
    OWNER_TYPE_PERSONAL_PLAYER, OWNER_TYPE_PLAYER, POI_CRATER, POI_PORT, POI_RIFT, POI_WARPHOLE,
    RESULT_SUCCESS, SKILL_CAT_ARMORCRAFTING, SKILL_CAT_BUTCHERY, SKILL_CAT_CONSTRUCTING,
    SKILL_CAT_COOKING, SKILL_CAT_DEFENSE, SKILL_CAT_FARMING, SKILL_CAT_GATHERING, SKILL_CAT_MELEE,
    SKILL_CAT_OIL_POISON, SKILL_CAT_PROCESS, SKILL_CAT_RANGED, SKILL_CAT_SURVIVAL,
    SKILL_CAT_WEAPONCRAFTING, TC_ABORT, TC_ACCEPT_MAILS, TC_ACCEPT_USER_MAILS, TC_ACTIONS,
    TC_ACTIVE_PERSONAL_REGION_WARPHOLE, TC_ADD_ITEMS_TO_WAREHOUSE, TC_ADD_ONS,
    TC_ADD_TO_FAVORITE_PRODUCTS, TC_APPEAR_ANIMAL, TC_APPEAR_ARTIFACT, TC_APPEAR_PLAYER,
    TC_ARCHIPELAGO, TC_ARTIFACT_BLUEPRINTS, TC_ARTIFACT_BUILT, TC_ARTIFACT_COMPLETED,
    TC_ARTIFACT_MATERIALS, TC_ATTACHABLE_ACCESSORIES, TC_AUTH, TC_AVAILABLE_EMOTIONS,
    TC_BATTLE_BEGUN, TC_BATTLE_ENDED, TC_BUILD_ARTIFACT, TC_BUILD_ESTIMATION, TC_BUY_PRODUCT,
    TC_CANCEL, TC_CANCELED, TC_CANCEL_CRAFTING, TC_CANCEL_FIRED_PROJECTILE,
    TC_CANCEL_TARGET_TITLE, TC_CHANGE_EQUIP_SLOT_TYPE, TC_CHEAT_FLAGS, TC_CHUNK, TC_CLOCK,
    TC_CLOSE_GATE, TC_COLLECT, TC_COLLECTED, TC_COLLECTIBLE, TC_COLLECTIBLE_CHANGED,
    TC_COMBAT_INTERACTION, TC_COMPLETE_ARTIFACT, TC_COSTS, TC_CRAFT, TC_CRAFTED,
    TC_CRAFT_ESTIMATION_INFO, TC_DAMAGED, TC_DASHED, TC_DECLARE_ESTATE, TC_DEFOGGED_CHUNKS,
    TC_DELETE_MAILS, TC_DELETE_USER_MAILS, TC_DEPART, TC_DESTRUCTING, TC_DESTRUCT_ARTIFACT,
    TC_DISAPPEAR_ENTITY, TC_DISAPPEAR_ENTITY_ON_TILE, TC_DISCOVERY_INFO, TC_DISCOVER_ANIMAL,
    TC_DRINK_WATER, TC_DUMP_ITEMS, TC_EMIGRATED, TC_ENTER_BATTLE, TC_ENTITY_DIED,
    TC_ENTITY_REVIVED, TC_EQUIP, TC_EQUIPMENTS, TC_ESTATE_GRIDS, TC_ESTATE_LICENSE,
    TC_ESTATE_LICENSES, TC_ESTIMATE_BUILD, TC_ESTIMATE_CRAFT, TC_EXIT_BATTLE, TC_EXPAND_ESTATE,
    TC_EXPLORED_POIS, TC_EXPLORE_POI, TC_EXP_GAINED, TC_EXTEND_ESTATE_ACTIVATION,
    TC_EXTINGUISH_BURNABLE, TC_FACTIONS, TC_FAILED, TC_FARMING_ENCYCLOPEDIA,
    TC_FATIGUE_VELOCITIES, TC_FIRE_BURNABLE, TC_GET_ACTIONS, TC_GET_ADD_ONS, TC_GET_ARCHIPELAGO,
    TC_GET_ARTIFACT, TC_GET_ARTIFACT_BLUEPRINTS, TC_GET_ATTACHABLE_ACCESSORIES,
    TC_GET_AVAILABLE_EMOTIONS, TC_GET_CHEAT_FLAGS, TC_GET_CLAN_CREATION_COSTS, TC_GET_CLOCK,
    TC_GET_COLLECTIBLE, TC_GET_DEFOGGED_CHUNKS, TC_GET_DISCOVERY_INFO, TC_GET_ENCYCLOPEDIA,
    TC_GET_ESTATE_LICENSES, TC_GET_ESTATE_LICENSE_BY_ID, TC_GET_EXPIRED_PRODUCTS,
    TC_GET_EXPLORED_POIS, TC_GET_FACTIONS, TC_GET_FAVORITE_PRODUCTS, TC_GET_INVENTORY,
    TC_GET_LAST_SEARCHED_TIME, TC_GET_MAILS, TC_GET_MEMOS, TC_GET_MISSIONS, TC_GET_MUSICS,
    TC_GET_NOMAD_INFO,
    TC_GET_PARTY, TC_GET_PERSONAL_REGION_INFO, TC_GET_PETS_INFO, TC_GET_PIONEER_GRADE_INFO,
    TC_GET_POI_COUNT, TC_GET_PURCHASED_PRODUCTS, TC_GET_QUESTS, TC_GET_QUEST_CATEGORIES,
    TC_GET_RECIPES, TC_GET_REGION, TC_GET_REGION_MAP_INFO, TC_GET_REGISTERED_PRODUCTS,
    TC_GET_RESISTANCE_EXP_CAPS, TC_GET_RETURNER_INFO, TC_GET_REVIVE_IMMEDIATELY_INFO,
    TC_GET_ROUTES, TC_GET_ROUTE_OF_ARCHIPELAGO, TC_GET_SAILING_BACK_COST, TC_GET_SECTION_ITEMS,
    TC_GET_SKILLS, TC_GET_SOCIAL, TC_GET_SOLD_PRODUCTS, TC_GET_STATISTICS, TC_GET_STATUS_EFFECTS,
    TC_GET_SUPPORT_REQUESTS, TC_GET_TITLES, TC_GET_WAREHOUSE, TC_GET_WARP_COSTS, TC_GET_WORKBENCH,
    TC_INVENTORY, TC_INVENTORY_INFOS, TC_INVENTORY_ORDER, TC_INVENTORY_UPDATED,
    TC_IS_WARPHOLE_AVAILABLE, TC_KEEPALIVE, TC_KICK_VISITOR, TC_LAST_SEARCHED_TIME,
    TC_LEARN_SKILL, TC_LEAVE_CLAN, TC_LOCK_OR_UNLOCK_ITEMS, TC_MAILS, TC_MAKE_CLAN,
    TC_MAKE_SECTION, TC_MARKET_COLLECT_ALL_PAYMENTS, TC_MARKET_COLLECT_PAYMENT,
    TC_MARK_MAILS_AS_READ, TC_MARK_USER_MAILS_AS_READ, TC_MEMBER, TC_MEMOS, TC_MISSION_INFOS,
    TC_MOUNT_AIR_BALLOON, TC_MOVE, TC_MOVE_ITEMS_IN_WAREHOUSE, TC_MUSICS, TC_NEAREST_POI,
    TC_NOMAD_INFO, TC_OCCUPIED,
    TC_OCCUPY_ARTIFACT_SITE, TC_OK, TC_OPEN_GATE, TC_OPEN_MAP, TC_PERSONAL_REGION_INFO,
    TC_PETS_INFO, TC_PIONEER_GRADE_INFO, TC_PLAYER_DISPLAY, TC_POINTS, TC_POI_COUNT,
    TC_POP_ITEMS_FROM_WAREHOUSE, TC_PRODUCTS, TC_PUT_IN_ITEM, TC_PUT_MATERIALS_INTO_ARTIFACT,
    TC_QUESTS, TC_QUEST_CATEGORIES, TC_READY, TC_RECIPES, TC_RECOMMENDED_STABLE_REGIONS,
    TC_RECOMMEND_ARCHIPELAGO, TC_RECOMMEND_REGION, TC_RECOMMEND_STABLE_REGIONS, TC_REGION,
    TC_REGION_MAP_INFO, TC_REGISTER_MULTIPLE_PRODUCTS, TC_REGISTER_PRODUCT, TC_REMOVE_DEATH_POINT,
    TC_REMOVE_ESTATE, TC_REMOVE_FROM_FAVORITE_PRODUCTS, TC_REMOVE_SECTION,
    TC_RENAME_WAREHOUSE_SECTION, TC_REPAIR_ARTIFACT, TC_REPAIR_IMMEDIATE, TC_REPAIR_ITEM,
    TC_REPLY_SEQUENCE_MARK, TC_REQUEST_NEAREST_POI, TC_RESEARCH_SKILL_CATEGORY,
    TC_RESISTANCE_EXP_CAPS, TC_REST_ON, TC_RETURNER_INFO, TC_RETURN_TO_CAMP, TC_RETURN_TO_ESTATE,
    TC_RETURN_TO_HOME, TC_REVIVE, TC_REVIVED, TC_REVIVE_IMMEDIATELY, TC_REVIVE_IMMEDIATELY_INFO,
    TC_REWARDED, TC_ROUTES, TC_ROUTES_OF_ARCHIPELAGO, TC_RUN_DOWN, TC_RUN_UP, TC_SAILING_BACK,
    TC_SAILING_BACK_COST, TC_SAY_IN_EXCLUSIVE_CHANNEL, TC_SEARCHED_POIS, TC_SEARCH_POIS,
    TC_SEARCH_PRODUCTS, TC_SELECT_BATTLE_TARGET, TC_SELECT_TITLE, TC_SEND_MAIL,
    TC_SET_ARTIFACT_ACCESS, TC_SET_AS_HOME, TC_SET_BASE_MOVE_SPEED, TC_SET_BLUEPRINT_LIKE,
    TC_SET_CHUNK, TC_SET_ESTATE_LICENSE, TC_SET_PERSONAL_REGION_ADMISSION, TC_SET_RECIPE_LIKE,
    TC_SET_RETURNING_POINT, TC_SET_SECTION_ITEM_ORDER, TC_SET_SECTION_ORDER, TC_SET_STORAGE_ITEM,
    TC_SHRINK_ESTATE, TC_SKILLS, TC_SOCIAL, TC_STATISTICS, TC_STATUS_EFFECTS, TC_SUPPORT_REQUESTS,
    TC_SURVIVAL, TC_TAKE_OUT_ITEM, TC_TELEPORTED, TC_TIMER, TC_TITLES, TC_TOGGLE_STATUS_EFFECT,
    TC_TOUCH, TC_TOUCHED, TC_TRAVEL_BY_REGION, TC_TRAVEL_BY_REGION_IN_ARCHIPELAGO,
    TC_TRAVEL_TO_RANDOM_PERSONAL_REGION, TC_TRAVEL_TO_STABLE_REGION, TC_TUTORIAL_EVENT,
    TC_UNMOUNT_AIR_BALLOON, TC_UNTRAIN_SKILL, TC_USE_BATTLE_ACTION, TC_USE_ITEM, TC_VISIT_ESTATE,
    TC_WALLET_UPDATED,
    TC_WAREHOUSE, TC_WARP, TC_WARP_COSTS, TC_WARP_TO_PERSONAL_REGION, TC_WARP_TO_PORT, TC_WASH,
    TC_WASH_BODY, TC_WELCOME, TC_WITHDRAW, TC_WORKBENCH, TELEPORT_TYPE_RETURNING,
    TELEPORT_TYPE_REVIVE, TELEPORT_TYPE_WARP,
};
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};
use tokio::sync::{mpsc, oneshot};
use uuid::Uuid;

use crate::catalog::GameCatalog;
use crate::db::{ArtifactRecord, EstateRecord, ExploredPoiRecord, ItemRecord};
use crate::state::{
    LiveAnimal, LiveClient, ServerState, DEFAULT_STARTING_T_STONE, ESTATE_LEASE_DAYS,
    MAIL_EXPIRY_DAYS, REST_FATIGUE_PER_SEC,
};

static NEXT_CONN_ID: AtomicU64 = AtomicU64::new(1);

const INVENTORY_MAX_SIZE: i32 = 200;
const MAX_COLLECT_RANGE_TILES: f32 = 12.0;
const WALK_NORMAL_SPEED: i32 = 320;
const WALK_BATTLE_SPEED: i32 = 300;
const RUN_NORMAL_SPEED: i32 = 660;
const RUN_BATTLE_SPEED: i32 = 480;
const RUN_ANIM_MULT: f32 = 1.45;
/// Canonical Durango specialization budget: 10 SP at Lv 1 + 3 SP per level (187 SP at Lv 60
/// out of ~3,213 total SP across all 13 trees), ensuring players specialize and trade on a
/// community server.
const INITIAL_SKILL_POINTS: i32 = 10;
const SKILL_POINTS_PER_LEVEL: i32 = 3;

fn item_record_to_msg(rec: &ItemRecord, catalog: &GameCatalog, now: f64) -> ItemMsg {
    let tags = catalog
        .prototype_tags(&rec.prototype_id)
        .into_iter()
        .map(|(id, level)| ItemTagMsg { id, level })
        .collect();
    let performances = catalog.item_performances(&rec.prototype_id, rec.level);
    ItemMsg {
        id: rec.item_id.clone(),
        name: rec.name.clone(),
        description: String::new(),
        icon: rec.icon.clone(),
        sub_icon: None,
        prototype: rec.prototype_id.clone(),
        level: rec.level,
        original_level: rec.level,
        size: rec.size.max(1),
        durability: GaugeMsg::constant(now, rec.durability, rec.max_durability.max(1.0)),
        founder_id: rec.entity_id.clone(),
        tags,
        performances,
        collectible_id: rec.collectible_id.clone(),
        generator_id: rec.generator_id.clone(),
        tradable: true,
    }
}

fn build_inventory_msgs(state: &ServerState, entity_id: &str, now: f64) -> (InventoryMsg, InventoryInfosMsg) {
    let records = state.db.list_items(entity_id).unwrap_or_default();
    let mut items = Vec::with_capacity(records.len());
    let mut locked = Vec::new();
    let mut order = Vec::with_capacity(records.len());
    for rec in &records {
        items.push(item_record_to_msg(rec, &state.catalog, now));
        order.push(rec.item_id.clone());
        if rec.locked {
            locked.push(rec.item_id.clone());
        }
    }
    let infos = InventoryInfosMsg {
        entity_id: entity_id.to_string(),
        max_size: INVENTORY_MAX_SIZE,
        locked_item_ids: locked,
        item_order: order,
    };
    let inv = InventoryMsg {
        entity_id: entity_id.to_string(),
        items,
        infos: infos.clone(),
        t_stone: state
            .db
            .get_t_stone(entity_id)
            .unwrap_or(DEFAULT_STARTING_T_STONE),
    };
    (inv, infos)
}

async fn send_fatigue_velocity(
    tx: &mpsc::Sender<Vec<u8>>,
    next_seq: &AtomicU32,
    now: f64,
    rate_per_sec: f32,
) {
    let msg = if rate_per_sec < -0.001 {
        FatigueVelocitiesMsg::resting(-rate_per_sec)
    } else {
        FatigueVelocitiesMsg::idle()
    };
    if let Ok(packed) = msg.pack() {
        if let Ok(frame) = encode_frame(
            now,
            next_seq.fetch_add(1, Ordering::Relaxed),
            0,
            TC_FATIGUE_VELOCITIES,
            &packed,
        ) {
            let _ = tx.send(frame).await;
        }
    }
}

async fn stop_resting_if_active(
    state: &ServerState,
    tx: &mpsc::Sender<Vec<u8>>,
    next_seq: &AtomicU32,
    entity_id: &str,
    now: f64,
) {
    let surv_snap = {
        let mut live = state.live_clients.write().await;
        live.get_mut(entity_id).and_then(|client| {
            if client.stop_rest(now) {
                Some((
                    client.to_survival_msg(now),
                    client.life,
                    client.current_stamina(now),
                    client.energy,
                    client.fatigue,
                ))
            } else {
                None
            }
        })
    };
    if let Some((surv, life, stam, eng, fat)) = surv_snap {
        let _ = state.db.update_survival(entity_id, life, stam, eng, fat);
        if let Ok(packed) = surv.pack() {
            if let Ok(sf) = encode_frame(
                now,
                next_seq.fetch_add(1, Ordering::Relaxed),
                0,
                TC_SURVIVAL,
                &packed,
            ) {
                let _ = tx.send(sf).await;
            }
        }
        send_fatigue_velocity(tx, next_seq, now, 0.0).await;
    }
}

fn build_container_inventory_msg(
    state: &ServerState,
    container_id: &str,
    section_name: Option<&str>,
    now: f64,
) -> InventoryMsg {
    let records = state
        .db
        .list_container_items(container_id, section_name)
        .unwrap_or_default();
    let mut items = Vec::with_capacity(records.len());
    let mut order = Vec::with_capacity(records.len());
    for rec in &records {
        items.push(item_record_to_msg(rec, &state.catalog, now));
        order.push(rec.item_id.clone());
    }
    let infos = InventoryInfosMsg {
        entity_id: container_id.to_string(),
        max_size: INVENTORY_MAX_SIZE,
        locked_item_ids: Vec::new(),
        item_order: order,
    };
    InventoryMsg {
        entity_id: container_id.to_string(),
        items,
        infos,
        t_stone: 0,
    }
}

fn build_mails_msg(state: &ServerState, entity_id: &str, now: f64) -> MailsMsg {
    let records = state.db.list_mails(entity_id).unwrap_or_default();
    let mut mails = Vec::new();
    let mut user_mails = Vec::new();
    for rec in records {
        let attached_items = rec
            .items
            .iter()
            .map(|it| item_record_to_msg(it, &state.catalog, now))
            .collect();
        let m = MailMsg {
            id: rec.mail_id,
            sent_at: rec.sent_at,
            sender_id: rec.sender_entity_id,
            mail_type: rec.mail_type,
            text: rec.text,
            t_stone: rec.t_stone,
            attached_items,
            accepted: rec.accepted,
            read: rec.read,
            expires_at: rec.expires_at,
        };
        if rec.is_user_mail {
            user_mails.push(m);
        } else {
            mails.push(m);
        }
    }
    MailsMsg { mails, user_mails }
}

fn build_warehouse_msg(state: &ServerState, container_id: &str) -> WarehouseMsg {
    let sections = state
        .db
        .list_warehouse_sections(container_id)
        .unwrap_or_default()
        .into_iter()
        .map(|(section_name, used_size)| WarehouseSectionInfoMsg {
            section_name,
            used_size,
            max_size: INVENTORY_MAX_SIZE,
        })
        .collect();
    WarehouseMsg {
        entity_id: container_id.to_string(),
        sections,
    }
}

fn build_equipments_msg(state: &ServerState, entity_id: &str, now: f64) -> EquipmentsMsg {
    let items_by_id: HashMap<String, ItemRecord> = state
        .db
        .list_items(entity_id)
        .unwrap_or_default()
        .into_iter()
        .map(|r| (r.item_id.clone(), r))
        .collect();
    let mut slots = BTreeMap::new();
    for (slot_name, item_id) in state.db.list_equipped(entity_id).unwrap_or_default() {
        if let Some(rec) = items_by_id.get(&item_id) {
            slots.insert(slot_name, item_record_to_msg(rec, &state.catalog, now));
        }
    }
    EquipmentsMsg {
        current_type: EQUIP_SLOT_1,
        slots,
    }
}

fn artifact_condition(durability: f32, max_durability: f32) -> i32 {
    let ratio = (durability / max_durability.max(1.0)).clamp(0.0, 1.0);
    if ratio <= 0.001 {
        3 // Broken
    } else if ratio < 0.3 {
        2 // Worn
    } else if ratio < 0.6 {
        1 // Old
    } else {
        0 // Normal
    }
}

const MAX_INVENTORY_ITEMS: usize = 200;

fn is_valid_equip_slot(slot: &str) -> bool {
    matches!(
        slot.to_ascii_lowercase().as_str(),
        "main"
            | "both"
            | "sub"
            | "weapon"
            | "head"
            | "body"
            | "hoody"
            | "hand"
            | "hands"
            | "gloves"
            | "foot"
            | "feet"
            | "shoes"
            | "back"
            | "bag"
            | "accessory"
            | "tool"
            | "waist"
    )
}

async fn is_entity_alive(state: &ServerState, eid: &str) -> bool {
    state
        .live_clients
        .read()
        .await
        .get(eid)
        .is_some_and(|c| c.is_alive)
}

fn is_estate_owner(state: &ServerState, eid: &str, rec: &EstateRecord) -> bool {
    if rec.owner_id == eid {
        return true;
    }
    if rec.owner_type == OWNER_TYPE_CLAN_ESTATE {
        return state
            .db
            .get_player_clan(eid)
            .ok()
            .flatten()
            .is_some_and(|(cid, _, _)| cid == rec.owner_id);
    }
    false
}

async fn validate_storage_artifact(
    state: &ServerState,
    eid: &str,
    authed_owner: &str,
    region_id: &str,
    container_id: &str,
) -> Result<ArtifactRecord, &'static str> {
    let art = state
        .db
        .get_artifact(container_id)
        .ok()
        .flatten()
        .ok_or("Container or warehouse does not exist.")?;
    if art.building_state != BUILDING_STATE_COMPLETED {
        return Err("Container is not yet completed.");
    }
    can_access_artifact(state, eid, authed_owner, region_id, &art, false).await?;
    Ok(art)
}

fn should_spawn_on_air_balloon(
    state: &ServerState,
    eid: &str,
    region_id: &str,
    is_alive: bool,
) -> bool {
    if !is_alive || region_role_for(region_id) != RegionRole::Personal {
        return false;
    }
    if !state.db.list_estates_by_owner(eid).unwrap_or_default().is_empty() {
        return false;
    }
    let storage = state.db.list_player_storage(eid).unwrap_or_default();
    if storage.contains_key("_balloon_landed") {
        return false;
    }
    if let Some(raw) = storage.get("personal_guide_progress") {
        if let Ok(val) = serde_json::from_slice::<serde_json::Value>(raw) {
            if let Some(arr) = val
                .pointer("/CompletedEvents/normal_new_user")
                .and_then(|v| v.as_array())
            {
                if arr
                    .iter()
                    .any(|v| v.as_str() == Some("todo_hotaired_balloon_landing"))
                {
                    return false;
                }
            }
        }
    }
    true
}

fn apply_live_boarding(
    app: &mut durango_wire::CharacterAppearance,
    boarding_on: i32,
    boarding_since: Option<f64>,
    boarding_until: Option<f64>,
    vehicle_entity_id: &str,
) {
    app.boarding_on = boarding_on;
    app.boarding_since = boarding_since;
    app.boarding_until = boarding_until;
    app.vehicle_entity_id = vehicle_entity_id.to_string();
}

async fn broadcast_player_display_for(
    state: &ServerState,
    eid: &str,
    region_id: &str,
    now: f64,
    next_seq: &AtomicU32,
) -> Result<(), durango_wire::WireError> {
    let ch = state.resolve_character(eid).await;
    let mut appearance = ch
        .as_ref()
        .map(|c| state.build_character_appearance(c))
        .unwrap_or_default();
    if let Some(lc) = state.live_clients.read().await.get(eid) {
        apply_live_boarding(
            &mut appearance,
            lc.boarding_on,
            lc.boarding_since,
            lc.boarding_until,
            &lc.vehicle_entity_id,
        );
    }
    let equip = build_equipments_msg(state, eid, now);
    let display = PlayerDisplayMsg {
        entity_id: eid.to_string(),
        equip: equip
            .slots
            .get("body")
            .or_else(|| equip.slots.get("hoody"))
            .map(|it| it.prototype.clone()),
        head: equip.slots.get("head").map(|it| it.prototype.clone()),
        appearance,
    };
    let disp_frame = encode_frame(
        now,
        next_seq.fetch_add(1, Ordering::Relaxed),
        0,
        TC_PLAYER_DISPLAY,
        &display.pack()?,
    )?;
    state.broadcast_region_all(region_id, &disp_frame).await;
    Ok(())
}

async fn can_access_artifact(
    state: &ServerState,
    eid: &str,
    authed_owner: &str,
    region_id: &str,
    art: &ArtifactRecord,
    require_owner: bool,
) -> Result<(), &'static str> {
    if art.region_id != region_id {
        return Err("Target structure is in another region.");
    }
    let player_pos = state
        .live_clients
        .read()
        .await
        .get(eid)
        .map(|c| (c.pos_x, c.pos_y));
    if let Some((px, py)) = player_pos {
        let cx = f32::from(art.tile_x) + f32::from(art.size_x.max(1)) * 0.5;
        let cy = f32::from(art.tile_y) + f32::from(art.size_y.max(1)) * 0.5;
        let dist = ((px / 200.0) - cx).hypot((py / 200.0) - cy);
        if dist > 16.0 {
            return Err("Structure is too far away.");
        }
    }
    let is_admin = state.is_admin_owner(authed_owner);
    if require_owner && art.founder_entity_id != eid && !is_admin {
        return Err("You do not own this structure.");
    }
    let now = ServerState::now_unix_secs();
    if let Ok(Some(estate)) = state.db.find_active_estate_for_tile(
        &art.region_id,
        art.tile_x,
        art.tile_y,
        art.size_x,
        art.size_y,
        now,
    ) {
        if estate.access_for_others == 0
            && !is_estate_owner(state, eid, &estate)
            && !is_admin
        {
            return Err("This structure is inside a private domain.");
        }
    }
    Ok(())
}

fn artifact_record_to_appear_msg(
    rec: &ArtifactRecord,
    catalog: &GameCatalog,
    now: f64,
) -> AppearArtifactMsg {
    let mut parts = catalog.artifact_default_parts(rec.entity_type, &rec.blueprint_id);
    if rec.is_lit {
        for val in parts.values_mut() {
            if !val.ends_with("_burning") {
                val.push_str("_burning");
            }
        }
    }
    let tags = catalog
        .artifact_workbench_tags(rec.entity_type, &rec.blueprint_id)
        .into_iter()
        .map(|(id, level)| ItemTagMsg { id, level })
        .collect();
    let max_d = rec.max_durability.max(1.0);
    let cur_d = rec.durability.clamp(0.0, max_d);
    AppearArtifactMsg {
        entity_id: rec.entity_id.clone(),
        entity_type: rec.entity_type,
        is_alive: true,
        tile_x: rec.tile_x,
        tile_y: rec.tile_y,
        size_x: rec.size_x,
        size_y: rec.size_y,
        height: rec.height,
        rotation: rec.rotation,
        building_state: rec.building_state,
        founder_entity_id: rec.founder_entity_id.clone(),
        level: rec.level,
        now_secs: now,
        parts,
        condition: artifact_condition(cur_d, max_d),
        durability: cur_d,
        max_durability: max_d,
        gate_opened: rec.gate_opened,
        tags,
    }
}

fn artifact_overlaps_chunks(art: &ArtifactRecord, chunks: &HashSet<(u8, u8)>) -> bool {
    let min_cx = (art.tile_x / 16) as u8;
    let max_cx = ((art.tile_x.saturating_add(art.size_x.max(1) - 1)) / 16) as u8;
    let min_cy = (art.tile_y / 16) as u8;
    let max_cy = ((art.tile_y.saturating_add(art.size_y.max(1) - 1)) / 16) as u8;
    for cx in min_cx..=max_cx {
        for cy in min_cy..=max_cy {
            if chunks.contains(&(cx, cy)) {
                return true;
            }
        }
    }
    false
}

fn rects_overlap(ax: u16, ay: u16, aw: u16, ah: u16, bx: u16, by: u16, bw: u16, bh: u16) -> bool {
    let ax2 = u32::from(ax) + u32::from(aw.max(1));
    let ay2 = u32::from(ay) + u32::from(ah.max(1));
    let bx2 = u32::from(bx) + u32::from(bw.max(1));
    let by2 = u32::from(by) + u32::from(bh.max(1));
    u32::from(ax) < bx2 && ax2 > u32::from(bx) && u32::from(ay) < by2 && ay2 > u32::from(by)
}

fn live_animal_to_appear_msg(a: &LiveAnimal, now: f64) -> AppearAnimalMsg {
    let (ax, ay) = a.current_pos(now);
    AppearAnimalMsg {
        entity_id: a.entity_id.clone(),
        entity_type: a.entity_type,
        is_alive: a.is_alive,
        base_scale: a.base_scale,
        level: a.level,
        motion_name: if a.is_alive {
            a.stand_motion.clone()
        } else {
            a.dead_motion.clone()
        },
        motion_option: if a.is_alive { 1 } else { 0 }, // 1 = LOOPING
        pos_x: ax,
        pos_y: ay,
        yaw: a.yaw,
        hp: a.hp,
        max_hp: a.max_hp,
        role: None,
        enemy_id: a.enemy_id.clone(),
        now_secs: now,
    }
}

fn build_combat_interaction_msg(
    entity_id: &str,
    target_id: &str,
    status: i64,
    look_at: bool,
    notice_attack_now: Option<f64>,
) -> CombatInteractionMsg {
    let mut details = BTreeMap::new();
    details.insert("status".to_string(), status);
    details.insert("look_at".to_string(), if look_at { 1 } else { 0 });
    if let Some(now) = notice_attack_now {
        details.insert("notice_attack".to_string(), (now * 1000.0) as i64);
    }
    CombatInteractionMsg {
        entity_id: entity_id.to_string(),
        target_id: target_id.to_string(),
        details,
    }
}

fn build_status_effects_msg(client: &LiveClient, now: f64) -> StatusEffectsMsg {
    let mut effects = Vec::new();
    if client.well_rested_until > now {
        effects.push(StatusEffectMsg {
            id: "energetic".to_string(),
            effect_id: "energetic".to_string(),
            level: 1,
            since: (client.well_rested_until - 600.0).max(0.0),
            until: client.well_rested_until,
            stacked: 1,
            duration_hidden: false,
            name_gettext: None,
        });
    }
    StatusEffectsMsg {
        entity_id: client.entity_id.clone(),
        effects,
    }
}

fn make_animal_pose_move(
    entity_id: &str,
    motion_name: &str,
    x: f32,
    y: f32,
    yaw: f32,
    now: f64,
    duration: f64,
    looping: bool,
) -> MoveMsg {
    MoveMsg {
        entity_id: entity_id.to_string(),
        movements: vec![Movement {
            motion_name: motion_name.to_string(),
            motion_option: if looping {
                MOTION_OPT_LOOPING
            } else {
                MOTION_OPT_NORMAL
            },
            playback_rate: 1.0,
            rot_speed: 540.0,
            path: vec![
                Location {
                    x,
                    y,
                    yaw,
                    time: now,
                    floor: 0,
                    height: 0.0,
                },
                Location {
                    x,
                    y,
                    yaw,
                    time: now + duration,
                    floor: 0,
                    height: 0.0,
                },
            ],
        }],
    }
}

/// Authoritative regional wildlife AI loop:
/// 1. Respawns dead/butchered wildlife after `wildlife_respawn_secs` at a fresh randomized dry-land tile (`AppearAnimal`).
/// 2. Proximity Aggro AI: aggressive predators (Carnivores) detect nearby players (6-tile vision cone / 3-tile scent),
///    roar a 1.5s Threat Telegraph with `CombatInteraction(Battle + notice_attack)` + `BattleBegun`, then chase & attack.
/// 3. Active Combat AI: smoothly chases from interpolated `current_pos(now)` with `LOOPING | ALIGN_TO_PATH`, plays varied
///    species attack clips (`attacks[]`), recovers back to `battle_stand_motion` after `0.8s` (`stand_at`), deals counter-damage
///    (`Damaged` with SmallBody/LargeBody + KnockBack/Blow, honoring dodge windows), and forgets aggro after 10s leash break.
/// 4. Peaceful dry-land wandering (`*_Walk` with `LOOPING | ALIGN_TO_PATH` -> `*_Stand`) when not in combat.
pub async fn run_wildlife_ai_loop(state: ServerState, interval_ms: u64) {
    let mut tick = tokio::time::interval(Duration::from_millis(interval_ms.max(100)));
    tick.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut seq: u32 = 900_000;
    let mut rng_state: u64 =
        ((ServerState::now_unix_secs() * 1_000_000.0) as u64) ^ 0x5EED_D1A0_2026_1123;
    let mut next_rand = || -> u64 {
        rng_state = rng_state
            .wrapping_mul(6364136223846793005)
            .wrapping_add(1442695040888963407);
        rng_state >> 16
    };

    enum AiEvent {
        RegionFrame(String, u32, Vec<u8>),
        DirectFrame(mpsc::Sender<Vec<u8>>, u32, Vec<u8>),
    }

    let mut decay_tick_counter: u64 = 0;

    loop {
        tick.tick().await;
        decay_tick_counter = decay_tick_counter.wrapping_add(1);
        if decay_tick_counter % 4 == 0 {
            let st_m = state.clone();
            tokio::spawn(async move {
                st_m.sample_metrics().await;
            });
        }
        let active_regions: HashSet<String> = {
            let live = state.live_clients.read().await;
            if live.is_empty() {
                continue;
            }
            live.values().map(|c| c.region_id.clone()).collect()
        };

        // Continuous resting recovery tick (every 2 ticks = ~1s) & Well-Rested expiry check:
        // Snapshots DB writes under lock and flushes after releasing live_clients!
        if decay_tick_counter % 2 == 0 {
            let rest_now = ServerState::now_unix_secs();
            let mut rest_updates: Vec<(mpsc::Sender<Vec<u8>>, SurvivalMsg, bool)> = Vec::new();
            let mut db_survival_flushes: Vec<(String, f32, f32, f32, f32)> = Vec::new();
            let mut expired_status_pushes: Vec<(mpsc::Sender<Vec<u8>>, StatusEffectsMsg)> = Vec::new();
            {
                let mut live = state.live_clients.write().await;
                for c in live.values_mut() {
                    if c.well_rested_until > 0.0 && rest_now >= c.well_rested_until {
                        c.well_rested_until = 0.0;
                        expired_status_pushes.push((c.tx.clone(), build_status_effects_msg(c, rest_now)));
                    }
                    let was_resting = c.is_resting;
                    if c.tick_rest(rest_now) || (was_resting && !c.is_alive) {
                        db_survival_flushes.push((
                            c.entity_id.clone(),
                            c.life,
                            c.current_stamina(rest_now),
                            c.energy,
                            c.fatigue,
                        ));
                        let done_resting = !c.is_resting
                            || !c.is_alive
                            || (c.fatigue <= 0.001
                                && c.life >= c.max_life - 0.01
                                && c.energy >= 99.99);
                        if done_resting {
                            c.is_resting = false;
                        }
                        rest_updates.push((c.tx.clone(), c.to_survival_msg(rest_now), done_resting));
                    }
                }
            }
            for (eid, life, stam, eng, fat) in db_survival_flushes {
                let _ = state.db.update_survival(&eid, life, stam, eng, fat);
            }
            for (cli_tx, surv, done_resting) in rest_updates {
                if let Ok(packed) = surv.pack() {
                    seq = seq.wrapping_add(1);
                    if let Ok(frame) = encode_frame(rest_now, seq, 0, TC_SURVIVAL, &packed) {
                        let _ = cli_tx.try_send(frame);
                    }
                }
                if done_resting {
                    if let Ok(packed) = FatigueVelocitiesMsg::idle().pack() {
                        seq = seq.wrapping_add(1);
                        if let Ok(fv) = encode_frame(rest_now, seq, 0, TC_FATIGUE_VELOCITIES, &packed) {
                            let _ = cli_tx.try_send(fv);
                        }
                    }
                }
            }
            for (cli_tx, st_msg) in expired_status_pushes {
                if let Ok(packed) = st_msg.pack() {
                    seq = seq.wrapping_add(1);
                    if let Ok(sf) = encode_frame(rest_now, seq, 0, TC_STATUS_EFFECTS, &packed) {
                        let _ = cli_tx.try_send(sf);
                    }
                }
            }
        }

        // Periodic wilderness building durability decay (~every 60 ticks = 30s) offloaded cleanly.
        if decay_tick_counter % 60 == 0 {
            let st_decay = state.clone();
            let regions_vec: Vec<String> = active_regions.iter().cloned().collect();
            tokio::spawn(async move {
                let decay_now = ServerState::now_unix_secs();
                let mut local_seq: u32 = 800_000;
                for region_id in regions_vec {
                    if let Ok(decayed) = st_decay
                        .db
                        .decay_wilderness_artifacts(&region_id, decay_now, 2.0)
                    {
                        for art in decayed {
                            let appear =
                                artifact_record_to_appear_msg(&art, &st_decay.catalog, decay_now);
                            if let Ok(sp) = appear.state_msg().pack() {
                                local_seq = local_seq.wrapping_add(1);
                                if let Ok(sf) = encode_frame(
                                    decay_now,
                                    local_seq,
                                    0,
                                    durango_wire::TC_ARTIFACT_STATE,
                                    &sp,
                                ) {
                                    st_decay.broadcast_region_all(&region_id, &sf).await;
                                }
                            }
                            if let Ok(dp) = appear.display_msg().pack() {
                                local_seq = local_seq.wrapping_add(1);
                                if let Ok(df) = encode_frame(
                                    decay_now,
                                    local_seq,
                                    0,
                                    durango_wire::TC_ARTIFACT_DISPLAY,
                                    &dp,
                                ) {
                                    st_decay.broadcast_region_all(&region_id, &df).await;
                                }
                            }
                        }
                    }
                }
            });
        }

        let now = ServerState::now_unix_secs();
        let mut events: Vec<AiEvent> = Vec::new();
        let mut ai_survival_flushes: Vec<(String, f32, f32, f32, f32)> = Vec::new();

        let mut region_terrains = HashMap::new();
        let mut region_herd_pts: HashMap<String, Vec<(u16, u16)>> = HashMap::new();
        for rid in &active_regions {
            let t = state.terrains.get_or_load(rid).await;
            let pts: Vec<(u16, u16)> = t
                .herds
                .all_points()
                .into_iter()
                .filter(|&(hx, hy)| t.is_safe_inland_land(hx, hy))
                .collect();
            region_herd_pts.insert(rid.clone(), pts);
            region_terrains.insert(rid.clone(), t);
        }

        {
            let mut animals = state.animals.write().await;
            let mut live_clients = state.live_clients.write().await;

            // Snapshot active aggro counts per player to handle multi-mob battle state cleanly (H4)
            let mut aggro_counts: HashMap<String, usize> = HashMap::new();
            for a in animals.values() {
                if a.is_alive && !a.butchered {
                    if let Some(ref tid) = a.enemy_id {
                        *aggro_counts.entry(tid.clone()).or_insert(0) += 1;
                    }
                }
            }

            for a in animals.values_mut() {
                if !active_regions.contains(&a.region_id) {
                    continue;
                }

                // 1. Respawn dead or butchered dinosaurs at a fresh randomized dry-land tile
                if !a.is_alive || a.butchered {
                    if a.respawn_at > 0.0 && now >= a.respawn_at {
                        if !a.butchered {
                            if let Ok(p) = (DisappearEntity {
                                entity_id: a.entity_id.clone(),
                            })
                            .pack()
                            {
                                events.push(AiEvent::RegionFrame(
                                    a.region_id.clone(),
                                    TC_DISAPPEAR_ENTITY,
                                    p,
                                ));
                            }
                        }
                        let (mut rx, mut ry) = (a.home_x, a.home_y);
                        if let Some(terrain) = region_terrains.get(&a.region_id) {
                            let max_tx = terrain.width.saturating_sub(3) as i32;
                            let max_ty = terrain.height.saturating_sub(3) as i32;
                            let herd_pts = region_herd_pts
                                .get(&a.region_id)
                                .map(Vec::as_slice)
                                .unwrap_or(&[]);
                            let (base_tx, base_ty) = if !herd_pts.is_empty() {
                                herd_pts[(next_rand() as usize) % herd_pts.len()]
                            } else {
                                (
                                    (a.home_x / 200.0).round().clamp(2.0, max_tx as f32) as u16,
                                    (a.home_y / 200.0).round().clamp(2.0, max_ty as f32) as u16,
                                )
                            };
                            for _ in 0..12 {
                                let ox = ((next_rand() % 11) as i32) - 5;
                                let oy = ((next_rand() % 11) as i32) - 5;
                                let cx = (i32::from(base_tx) + ox).clamp(2, max_tx) as u16;
                                let cy = (i32::from(base_ty) + oy).clamp(2, max_ty) as u16;
                                if terrain.is_safe_inland_land(cx, cy) {
                                    rx = f32::from(cx) * 200.0;
                                    ry = f32::from(cy) * 200.0;
                                    break;
                                }
                            }
                        }
                        a.hp = a.max_hp;
                        a.home_x = rx;
                        a.home_y = ry;
                        a.pos_x = rx;
                        a.pos_y = ry;
                        a.walk_from_x = rx;
                        a.walk_from_y = ry;
                        a.walk_start_at = 0.0;
                        a.walk_end_at = 0.0;
                        a.yaw = (next_rand() % 360) as f32;
                        a.is_alive = true;
                        a.butchered = false;
                        a.butcher_stock.clear();
                        a.enemy_id = None;
                        a.respawn_at = 0.0;
                        a.stop_walking_at = 0.0;
                        a.stand_at = 0.0;
                        a.threat_telegraph_until = 0.0;
                        a.aggro_seen_at = 0.0;
                        a.next_attack_at = 0.0;
                        a.next_wander_at = now + 1.5;
                        if let Ok(p) = live_animal_to_appear_msg(a, now).pack() {
                            events.push(AiEvent::RegionFrame(
                                a.region_id.clone(),
                                TC_APPEAR_ANIMAL,
                                p,
                            ));
                        }
                    }
                    continue;
                }

                // 1b. Post-attack pose recovery: return smoothly to battle_stand / stand after attack clip ends
                if a.stand_at > 0.0 && now >= a.stand_at {
                    a.stand_at = 0.0;
                    let (ax, ay) = a.current_pos(now);
                    let clip = if a.enemy_id.is_some() {
                        &a.battle_stand_motion
                    } else {
                        &a.stand_motion
                    };
                    if let Ok(p) =
                        make_animal_pose_move(&a.entity_id, clip, ax, ay, a.yaw, now, 2.0, true)
                            .pack()
                    {
                        events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                    }
                    continue;
                }
                if a.stand_at > 0.0 {
                    continue;
                }

                // 2a. Proximity Aggro for aggressive predators (Carnivores)
                if a.enemy_id.is_none() && a.is_aggressive {
                    let (ax, ay) = a.current_pos(now);
                    let mut detected: Option<(String, f32, f32, f32)> = None;
                    for (peid, c) in live_clients.iter() {
                        if !c.is_alive || c.region_id != a.region_id {
                            continue;
                        }
                        let ddx = c.pos_x - ax;
                        let ddy = c.pos_y - ay;
                        let dist = ddx.hypot(ddy);
                        if dist > 1200.0 {
                            continue; // outside 6-tile aggro range
                        }
                        let angle_to_player = ddx.atan2(ddy).to_degrees().rem_euclid(360.0);
                        let mut diff = (angle_to_player - a.yaw).abs();
                        if diff > 180.0 {
                            diff = 360.0 - diff;
                        }
                        if dist <= 600.0 || diff <= 90.0 {
                            if detected.as_ref().is_none_or(|d| dist < d.3) {
                                detected = Some((peid.clone(), c.pos_x, c.pos_y, dist));
                            }
                        }
                    }
                    if let Some((target_eid, px, py, _)) = detected {
                        let yaw = (px - ax).atan2(py - ay).to_degrees().rem_euclid(360.0);
                        a.pos_x = ax;
                        a.pos_y = ay;
                        a.walk_from_x = ax;
                        a.walk_from_y = ay;
                        a.walk_start_at = 0.0;
                        a.walk_end_at = 0.0;
                        a.stop_walking_at = 0.0;
                        a.yaw = yaw;
                        a.enemy_id = Some(target_eid.clone());
                        a.aggro_seen_at = now;
                        a.threat_telegraph_until = now + 1.5;
                        *aggro_counts.entry(target_eid.clone()).or_insert(0) += 1;

                        if let Ok(p) = make_animal_pose_move(
                            &a.entity_id,
                            &a.battle_stand_motion,
                            ax,
                            ay,
                            yaw,
                            now,
                            1.5,
                            true,
                        )
                        .pack()
                        {
                            events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                        }
                        if let Ok(p) = build_combat_interaction_msg(
                            &a.entity_id,
                            &target_eid,
                            ANIMAL_STATUS_BATTLE,
                            true,
                            Some(now),
                        )
                        .pack()
                        {
                            events.push(AiEvent::RegionFrame(
                                a.region_id.clone(),
                                TC_COMBAT_INTERACTION,
                                p,
                            ));
                        }
                        if let Some(c) = live_clients.get_mut(&target_eid) {
                            if !c.in_battle {
                                c.in_battle = true;
                                c.battle_target_id = Some(a.entity_id.clone());
                                if let Ok(p) = (BattleBegunMsg {
                                    entity_id: target_eid.clone(),
                                    event_at: now,
                                    enemy_id: a.entity_id.clone(),
                                    start_damaged: false,
                                })
                                .pack()
                                {
                                    events.push(AiEvent::DirectFrame(
                                        c.tx.clone(),
                                        TC_BATTLE_BEGUN,
                                        p,
                                    ));
                                }
                            }
                        }
                        continue;
                    }
                }

                // 2b. Combat AI: chase and counter-attack aggro target
                if let Some(target_eid) = a.enemy_id.clone() {
                    let (ax, ay) = a.current_pos(now);
                    let player_info = live_clients
                        .get(&target_eid)
                        .filter(|c| c.region_id == a.region_id && c.is_alive)
                        .map(|c| (c.pos_x, c.pos_y));
                    let Some((px, py)) = player_info else {
                        a.enemy_id = None;
                        a.stop_walking_at = 0.0;
                        a.threat_telegraph_until = 0.0;
                        a.aggro_seen_at = 0.0;
                        a.next_wander_at = now + 1.5;
                        if let Some(cnt) = aggro_counts.get_mut(&target_eid) {
                            *cnt = cnt.saturating_sub(1);
                        }
                        if let Ok(p) = build_combat_interaction_msg(
                            &a.entity_id,
                            &target_eid,
                            ANIMAL_STATUS_PEACE,
                            false,
                            None,
                        )
                        .pack()
                        {
                            events.push(AiEvent::RegionFrame(
                                a.region_id.clone(),
                                TC_COMBAT_INTERACTION,
                                p,
                            ));
                        }
                        if aggro_counts.get(&target_eid).copied().unwrap_or(0) == 0 {
                            if let Some(c) = live_clients.get_mut(&target_eid) {
                                if c.in_battle {
                                    c.in_battle = false;
                                    c.battle_target_id = None;
                                    if let Ok(p) = (BattleEndedMsg {
                                        entity_id: target_eid.clone(),
                                        time: now,
                                    })
                                    .pack()
                                    {
                                        events.push(AiEvent::DirectFrame(
                                            c.tx.clone(),
                                            TC_BATTLE_ENDED,
                                            p,
                                        ));
                                    }
                                }
                            }
                        }
                        continue;
                    };

                    let home_dist = (px - a.home_x).hypot(py - a.home_y);
                    let cur_dist = (px - ax).hypot(py - ay);
                    if home_dist > 2800.0 && cur_dist > 1600.0 {
                        if a.aggro_seen_at > 0.0 && now - a.aggro_seen_at > 10.0 {
                            // Leash break: smoothly return toward home instead of teleporting/freezing
                            a.enemy_id = None;
                            a.threat_telegraph_until = 0.0;
                            a.aggro_seen_at = 0.0;
                            a.next_wander_at = now + 2.5;
                            if let Some(cnt) = aggro_counts.get_mut(&target_eid) {
                                *cnt = cnt.saturating_sub(1);
                            }
                            let hdx = a.home_x - ax;
                            let hdy = a.home_y - ay;
                            let hdist = hdx.hypot(hdy);
                            if hdist > 100.0 {
                                let ret_secs = (hdist / 360.0).clamp(1.0, 12.0) as f64;
                                let hyaw = hdx.atan2(hdy).to_degrees().rem_euclid(360.0);
                                a.walk_from_x = ax;
                                a.walk_from_y = ay;
                                a.walk_start_at = now;
                                a.walk_end_at = now + ret_secs;
                                a.pos_x = a.home_x;
                                a.pos_y = a.home_y;
                                a.yaw = hyaw;
                                a.stop_walking_at = now + ret_secs;
                                let mv = MoveMsg {
                                    entity_id: a.entity_id.clone(),
                                    movements: vec![Movement {
                                        motion_name: a.move_motion.clone(),
                                        motion_option: MOTION_OPT_LOOP_ALIGN,
                                        playback_rate: 1.0,
                                        rot_speed: 540.0,
                                        path: vec![
                                            Location {
                                                x: ax,
                                                y: ay,
                                                yaw: hyaw,
                                                time: now,
                                                floor: 0,
                                                height: 0.0,
                                            },
                                            Location {
                                                x: a.home_x,
                                                y: a.home_y,
                                                yaw: hyaw,
                                                time: now + ret_secs,
                                                floor: 0,
                                                height: 0.0,
                                            },
                                        ],
                                    }],
                                };
                                if let Ok(p) = mv.pack() {
                                    events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                                }
                            } else if let Ok(p) = make_animal_pose_move(
                                &a.entity_id,
                                &a.stand_motion,
                                ax,
                                ay,
                                a.yaw,
                                now,
                                2.0,
                                true,
                            )
                            .pack()
                            {
                                events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                            }
                            if let Ok(p) = build_combat_interaction_msg(
                                &a.entity_id,
                                &target_eid,
                                ANIMAL_STATUS_PEACE,
                                false,
                                None,
                            )
                            .pack()
                            {
                                events.push(AiEvent::RegionFrame(
                                    a.region_id.clone(),
                                    TC_COMBAT_INTERACTION,
                                    p,
                                ));
                            }
                            if aggro_counts.get(&target_eid).copied().unwrap_or(0) == 0 {
                                if let Some(c) = live_clients.get_mut(&target_eid) {
                                    if c.in_battle {
                                        c.in_battle = false;
                                        c.battle_target_id = None;
                                        if let Ok(p) = (BattleEndedMsg {
                                            entity_id: target_eid.clone(),
                                            time: now,
                                        })
                                        .pack()
                                        {
                                            events.push(AiEvent::DirectFrame(
                                                c.tx.clone(),
                                                TC_BATTLE_ENDED,
                                                p,
                                            ));
                                        }
                                    }
                                }
                            }
                        }
                        continue;
                    }
                    a.aggro_seen_at = now;

                    // Wait out the 1.5s Threat Telegraph Roar before lunging
                    if now < a.threat_telegraph_until {
                        continue;
                    }

                    let ddx = px - ax;
                    let ddy = py - ay;
                    let dist = ddx.hypot(ddy);
                    let yaw = ddx.atan2(ddy).to_degrees().rem_euclid(360.0);

                    if dist > 420.0 {
                        if a.stop_walking_at == 0.0 || now >= a.stop_walking_at {
                            let step = (dist - 220.0).max(80.0);
                            let mut nx = ax + (ddx / dist) * step;
                            let mut ny = ay + (ddy / dist) * step;
                            let mut dry_ok = true;
                            if let Some(terrain) = region_terrains.get(&a.region_id) {
                                let max_tx = terrain.width.saturating_sub(2) as i32;
                                let max_ty = terrain.height.saturating_sub(2) as i32;
                                let mut found_dry = None;
                                let ux = ddx / dist;
                                let uy = ddy / dist;
                                // Try direct line fractions, then angular skirts around water edges
                                'search_dry: for &deg in &[0.0f32, 30.0, -30.0, 60.0, -60.0] {
                                    let rad = deg.to_radians();
                                    let (s, c_ang) = rad.sin_cos();
                                    let rx = ux * c_ang - uy * s;
                                    let ry = ux * s + uy * c_ang;
                                    for frac in [1.0f32, 0.75, 0.5, 0.25] {
                                        let cx = ax + rx * (step * frac);
                                        let cy = ay + ry * (step * frac);
                                        let tx = (cx / 200.0).round().clamp(1.0, max_tx as f32) as u16;
                                        let ty = (cy / 200.0).round().clamp(1.0, max_ty as f32) as u16;
                                        if terrain.is_dry_land(tx, ty) {
                                            found_dry = Some((cx, cy));
                                            break 'search_dry;
                                        }
                                    }
                                }
                                if let Some((dx_ok, dy_ok)) = found_dry {
                                    nx = dx_ok;
                                    ny = dy_ok;
                                } else {
                                    dry_ok = false;
                                }
                            }
                            if dry_ok {
                                let actual_step = (nx - ax).hypot(ny - ay);
                                let chase_secs = (actual_step / 420.0).clamp(0.35, 2.2) as f64;
                                a.walk_from_x = ax;
                                a.walk_from_y = ay;
                                a.walk_start_at = now;
                                a.walk_end_at = now + chase_secs;
                                a.pos_x = nx;
                                a.pos_y = ny;
                                a.yaw = yaw;
                                a.stop_walking_at = now + chase_secs;
                                let mv = MoveMsg {
                                    entity_id: a.entity_id.clone(),
                                    movements: vec![Movement {
                                        motion_name: a.move_motion.clone(),
                                        motion_option: MOTION_OPT_LOOP_ALIGN,
                                        playback_rate: 1.35,
                                        rot_speed: 540.0,
                                        path: vec![
                                            Location {
                                                x: ax,
                                                y: ay,
                                                yaw,
                                                time: now,
                                                floor: 0,
                                                height: 0.0,
                                            },
                                            Location {
                                                x: nx,
                                                y: ny,
                                                yaw,
                                                time: now + chase_secs,
                                                floor: 0,
                                                height: 0.0,
                                            },
                                        ],
                                    }],
                                };
                                if let Ok(p) = mv.pack() {
                                    events.push(AiEvent::RegionFrame(
                                        a.region_id.clone(),
                                        TC_MOVE,
                                        p,
                                    ));
                                }
                            }
                        }
                    } else if now >= a.next_attack_at {
                        // In melee reach (even if mid-chase walk): halt cleanly at current pos & attack
                        a.pos_x = ax;
                        a.pos_y = ay;
                        a.walk_from_x = ax;
                        a.walk_from_y = ay;
                        a.walk_start_at = 0.0;
                        a.walk_end_at = 0.0;
                        a.yaw = yaw;
                        a.stop_walking_at = 0.0;
                        a.stand_at = now + 0.8;
                        let cd = state
                            .config
                            .wildlife_attack_cooldown_secs
                            .min(f64::from(a.attack_cooltime))
                            .max(0.15);
                        a.next_attack_at = now + cd;

                        let atk_clip = if !a.attacks.is_empty() {
                            &a.attacks[(next_rand() as usize) % a.attacks.len()]
                        } else {
                            &a.attack_motion
                        };
                        if let Ok(p) = build_combat_interaction_msg(
                            &a.entity_id,
                            &target_eid,
                            ANIMAL_STATUS_BATTLE,
                            true,
                            Some(now),
                        )
                        .pack()
                        {
                            events.push(AiEvent::RegionFrame(
                                a.region_id.clone(),
                                TC_COMBAT_INTERACTION,
                                p,
                            ));
                        }
                        let atk_mv = make_animal_pose_move(
                            &a.entity_id,
                            atk_clip,
                            ax,
                            ay,
                            yaw,
                            now,
                            0.8,
                            false,
                        );
                        if let Ok(p) = atk_mv.pack() {
                            events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                        }

                        if let Some(player) = live_clients.get_mut(&target_eid) {
                            let dodged = now - player.last_dodge_at < 1.2;
                            let dmg_val = if dodged { 0 } else { a.attack_damage };
                            if !dodged {
                                player.life = (player.life - dmg_val as f32).max(0.0);
                            }
                            let died = player.life <= 0.0;
                            if died {
                                player.stop_rest(now);
                                player.is_alive = false;
                                player.in_battle = false;
                                player.battle_target_id = None;
                                player.last_battle_action_at = 0.0;
                                a.enemy_id = None;
                                a.next_wander_at = now + 2.0;
                                if let Some(cnt) = aggro_counts.get_mut(&target_eid) {
                                    *cnt = cnt.saturating_sub(1);
                                }
                                if let Ok(p) = build_combat_interaction_msg(
                                    &a.entity_id,
                                    &target_eid,
                                    ANIMAL_STATUS_PEACE,
                                    false,
                                    None,
                                )
                                .pack()
                                {
                                    events.push(AiEvent::RegionFrame(
                                        a.region_id.clone(),
                                        TC_COMBAT_INTERACTION,
                                        p,
                                    ));
                                }
                            }
                            let cur_stam = player.current_stamina(now);
                            ai_survival_flushes.push((
                                target_eid.clone(),
                                player.life,
                                cur_stam,
                                player.energy,
                                player.fatigue,
                            ));

                            let body_atk = if a.size_level >= 4 {
                                ATTACK_TYPE_LARGE_BODY
                            } else {
                                ATTACK_TYPE_SMALL_BODY
                            };
                            let hit_fx = if dodged {
                                0
                            } else if a.size_level >= 4 {
                                DAMAGE_EFFECT_BLOW
                            } else {
                                DAMAGE_EFFECT_KNOCK_BACK
                            };
                            let damaged = DamagedMsg {
                                victim_id: target_eid.clone(),
                                attacker_id: a.entity_id.clone(),
                                damage: DamageMsg {
                                    result: if dodged {
                                        DAMAGE_RESULT_EVADE
                                    } else {
                                        DAMAGE_RESULT_HIT
                                    },
                                    value: dmg_val,
                                    part: BODY_PART_BODY,
                                    direction: DAMAGE_DIR_FRONT,
                                    attack_type: body_atk,
                                    effects: hit_fx,
                                },
                                event_at: now,
                            };
                            if let Ok(p) = damaged.pack() {
                                events.push(AiEvent::RegionFrame(
                                    a.region_id.clone(),
                                    TC_DAMAGED,
                                    p,
                                ));
                            }

                            let psurv = player.to_survival_msg(now);
                            if let Ok(p) = psurv.pack() {
                                events.push(AiEvent::DirectFrame(
                                    player.tx.clone(),
                                    TC_SURVIVAL,
                                    p,
                                ));
                            }

                            if died {
                                if let Ok(p) = (EntityDiedMsg {
                                    entity_id: target_eid.clone(),
                                    time: now,
                                })
                                .pack()
                                {
                                    events.push(AiEvent::RegionFrame(
                                        a.region_id.clone(),
                                        TC_ENTITY_DIED,
                                        p,
                                    ));
                                }
                                if let Ok(p) = (BattleEndedMsg {
                                    entity_id: target_eid.clone(),
                                    time: now,
                                })
                                .pack()
                                {
                                    events.push(AiEvent::DirectFrame(
                                        player.tx.clone(),
                                        TC_BATTLE_ENDED,
                                        p,
                                    ));
                                }
                            }
                        }
                    }
                    continue;
                }

                // 3. Peaceful wandering: transition back to looping stand animation when walk finishes
                if a.stop_walking_at > 0.0 && now >= a.stop_walking_at {
                    a.stop_walking_at = 0.0;
                    a.walk_start_at = 0.0;
                    a.walk_end_at = 0.0;
                    a.next_wander_at = now + 2.0 + ((next_rand() % 30) as f64) * 0.1;
                    let stand_mv = make_animal_pose_move(
                        &a.entity_id,
                        &a.stand_motion,
                        a.pos_x,
                        a.pos_y,
                        a.yaw,
                        now,
                        2.0,
                        true,
                    );
                    if let Ok(p) = stand_mv.pack() {
                        events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                    }
                    continue;
                }

                // Start a new short stroll around home_x/home_y on dry land with LOOPING | ALIGN_TO_PATH
                if a.stop_walking_at == 0.0 && now >= a.next_wander_at {
                    let Some(terrain) = region_terrains.get(&a.region_id) else {
                        continue;
                    };
                    let max_tx = terrain.width.saturating_sub(2) as i32;
                    let max_ty = terrain.height.saturating_sub(2) as i32;
                    let mut chosen: Option<(f32, f32)> = None;
                    for _ in 0..8 {
                        let dx = ((next_rand() % 11) as i32) - 5; // -5..=+5 tiles
                        let dy = ((next_rand() % 11) as i32) - 5;
                        if dx == 0 && dy == 0 {
                            continue;
                        }
                        let tx = ((a.home_x / 200.0).round() as i32 + dx).clamp(2, max_tx) as u16;
                        let ty = ((a.home_y / 200.0).round() as i32 + dy).clamp(2, max_ty) as u16;
                        let cur_tx = (a.pos_x / 200.0).round().clamp(2.0, max_tx as f32) as u16;
                        let cur_ty = (a.pos_y / 200.0).round().clamp(2.0, max_ty as f32) as u16;
                        let mid_x = (cur_tx + tx) / 2;
                        let mid_y = (cur_ty + ty) / 2;
                        if terrain.is_safe_inland_land(tx, ty) && terrain.is_dry_land(mid_x, mid_y) {
                            chosen = Some((f32::from(tx) * 200.0, f32::from(ty) * 200.0));
                            break;
                        }
                    }
                    if let Some((dest_x, dest_y)) = chosen {
                        let (start_x, start_y) = a.current_pos(now);
                        let ddx = dest_x - start_x;
                        let ddy = dest_y - start_y;
                        let dist = ddx.hypot(ddy);
                        if dist < 50.0 {
                            a.next_wander_at = now + 2.0;
                            continue;
                        }
                        let speed = 260.0f32;
                        let walk_secs = (dist / speed).clamp(1.2, 6.0) as f64;
                        let yaw = ddx.atan2(ddy).to_degrees().rem_euclid(360.0);
                        a.walk_from_x = start_x;
                        a.walk_from_y = start_y;
                        a.walk_start_at = now;
                        a.walk_end_at = now + walk_secs;
                        a.pos_x = dest_x;
                        a.pos_y = dest_y;
                        a.yaw = yaw;
                        a.stop_walking_at = now + walk_secs;
                        let walk_mv = MoveMsg {
                            entity_id: a.entity_id.clone(),
                            movements: vec![Movement {
                                motion_name: a.move_motion.clone(),
                                motion_option: MOTION_OPT_LOOP_ALIGN,
                                playback_rate: 1.0,
                                rot_speed: 540.0,
                                path: vec![
                                    Location {
                                        x: start_x,
                                        y: start_y,
                                        yaw,
                                        time: now,
                                        floor: 0,
                                        height: 0.0,
                                    },
                                    Location {
                                        x: dest_x,
                                        y: dest_y,
                                        yaw,
                                        time: now + walk_secs,
                                        floor: 0,
                                        height: 0.0,
                                    },
                                ],
                            }],
                        };
                        if let Ok(p) = walk_mv.pack() {
                            events.push(AiEvent::RegionFrame(a.region_id.clone(), TC_MOVE, p));
                        }
                    } else {
                        a.next_wander_at = now + 3.0;
                    }
                }
            }
        }

        for (eid, life, cur_stam, energy, fatigue) in ai_survival_flushes {
            let _ = state
                .db
                .update_survival(&eid, life, cur_stam, energy, fatigue);
        }

        for ev in events {
            seq = seq.wrapping_add(1);
            match ev {
                AiEvent::RegionFrame(region_id, tc, packed) => {
                    if let Ok(frame) = encode_frame(now, seq, 0, tc, &packed) {
                        state.broadcast_region_all(&region_id, &frame).await;
                    }
                }
                AiEvent::DirectFrame(tx, tc, packed) => {
                    if let Ok(frame) = encode_frame(now, seq, 0, tc, &packed) {
                        let _ = tx.try_send(frame);
                    }
                }
            }
        }
    }
}

fn is_weapon_slot(slot_name: &str) -> bool {
    slot_name.eq_ignore_ascii_case("main")
        || slot_name.eq_ignore_ascii_case("both")
        || slot_name.eq_ignore_ascii_case("weapon")
}

fn build_actions_msg(state: &ServerState, entity_id: &str) -> ActionsMsg {
    let items_by_id: HashMap<String, ItemRecord> = state
        .db
        .list_items(entity_id)
        .unwrap_or_default()
        .into_iter()
        .map(|r| (r.item_id.clone(), r))
        .collect();
    let equipped = state.db.list_equipped(entity_id).unwrap_or_default();
    let mut tags: Vec<String> = Vec::new();
    let mut has_weapon_slot = false;
    for (slot_name, item_id) in &equipped {
        if is_weapon_slot(slot_name) {
            has_weapon_slot = true;
        }
        if let Some(rec) = items_by_id.get(item_id) {
            for (tag_id, _) in state.catalog.prototype_tags(&rec.prototype_id) {
                tags.push(tag_id);
            }
        }
    }
    let mut specs = state.catalog.battle_actions_for_tags(&tags);
    // If the player equipped a starter weapon/knife whose prototype lacks a specific weapon tag
    // (e.g. tool_stone_knife in headless tests), still grant onehand sword actions!
    if has_weapon_slot
        && specs
            .first()
            .is_some_and(|s| s.id.starts_with("barehand_"))
    {
        specs = state
            .catalog
            .battle_actions_for_tags(&["sword_onehand".to_string()]);
    }
    ActionsMsg {
        battle_actions: specs
            .into_iter()
            .map(|s| ActionStatusMsg {
                id: s.id,
                stamina: s.stamina,
                cooltime: s.cooltime,
            })
            .collect(),
    }
}

const ALL_SKILL_CATEGORIES: [i32; 13] = [
    SKILL_CAT_SURVIVAL,
    SKILL_CAT_MELEE,
    SKILL_CAT_RANGED,
    SKILL_CAT_DEFENSE,
    SKILL_CAT_BUTCHERY,
    SKILL_CAT_GATHERING,
    SKILL_CAT_COOKING,
    SKILL_CAT_WEAPONCRAFTING,
    SKILL_CAT_ARMORCRAFTING,
    SKILL_CAT_CONSTRUCTING,
    SKILL_CAT_FARMING,
    SKILL_CAT_PROCESS,
    SKILL_CAT_OIL_POISON,
];

fn cat_level_from_exp(exp: i32) -> i32 {
    ((exp.max(0) / 60) + 1).clamp(1, 60)
}

fn region_role_for(region_id: &str) -> RegionRole {
    if let Some(meta) = crate::state::lookup_region_template(region_id) {
        return meta.role;
    }
    if region_id.starts_with("tamed-")
        || region_id.starts_with("pe")
        || region_id.starts_with("personal_")
    {
        RegionRole::Personal
    } else if region_id.starts_with("unstable-")
        || region_id.starts_with("ri")
        || region_id.starts_with("ra")
        || region_id.starts_with("ua")
        || region_id.starts_with("sn")
    {
        RegionRole::Risky
    } else {
        RegionRole::Rural
    }
}

async fn build_region_msg(state: &ServerState, region_id: &str) -> RegionMsg {
    let canon_id = crate::state::format_region_code(region_id);
    let effective_id = if canon_id.is_empty() {
        crate::state::DEFAULT_REGION_CODE.to_string()
    } else {
        canon_id
    };
    let terrain = state.terrains.get_or_load(&effective_id).await;
    let template_id = crate::state::lookup_region_template(&effective_id)
        .map(|m| m.template_id.to_string())
        .unwrap_or_else(|| terrain.template_id.clone());
    RegionMsg {
        id: effective_id.clone(),
        terrain_id: terrain.id.clone(),
        template_id,
        role: region_role_for(&effective_id),
        name: Some(crate::state::region_display_name(&effective_id)),
        created_at: 0.0,
    }
}

fn build_archipelago_routes(state: &ServerState) -> Vec<ArchipelagoRouteMsg> {
    let available: HashSet<String> = state
        .terrains
        .list_available_terrains()
        .into_iter()
        .collect();
    let mut routes = Vec::new();
    for meta in crate::state::KNOWN_REGION_TEMPLATES {
        if meta.role != RegionRole::Risky {
            continue;
        }
        if !available.is_empty() && !available.contains(meta.terrain_id) {
            continue;
        }
        routes.push(ArchipelagoRouteMsg {
            level: meta.level,
            biome: meta.biome,
            unstable_factor: 1,
            archipelago_id: meta.zone_code.to_string(),
            included_region_ids: vec![meta.zone_code.to_string()],
        });
        // Also populate the Lv.20 TemperateForest node (`20Te`) on the Sea Map using sn20snow
        if meta.terrain_id == "sn20snow" {
            routes.push(ArchipelagoRouteMsg {
                level: 20,
                biome: 0, // TemperateForest
                unstable_factor: 1,
                archipelago_id: meta.zone_code.to_string(),
                included_region_ids: vec![meta.zone_code.to_string()],
            });
        }
    }
    routes
}

fn build_routes_msg(state: &ServerState) -> RoutesMsg {
    let available: HashSet<String> = state
        .terrains
        .list_available_terrains()
        .into_iter()
        .collect();
    let mut by_role: BTreeMap<i32, BTreeMap<String, Vec<String>>> = BTreeMap::new();
    for meta in crate::state::KNOWN_REGION_TEMPLATES {
        if !available.is_empty() && !available.contains(meta.terrain_id) {
            continue;
        }
        by_role
            .entry(meta.role as i32)
            .or_default()
            .entry(meta.template_id.to_string())
            .or_default()
            .push(meta.zone_code.to_string());
    }
    // Fixed beginner nodes on WorldRoutesBeginnerStableArea.cs:
    // - tropical_event_ancora_01 (RegionRole::Risky = 4) -> unstable-tropical-lv18-01
    // - grass_company_safehouse_01 (RegionRole::Safehouse = 7) -> tamed-grassland-lv10-02
    by_role
        .entry(RegionRole::Risky as i32)
        .or_default()
        .entry("tropical_event_ancora_01".to_string())
        .or_default()
        .push("unstable-tropical-lv18-01".to_string());
    by_role
        .entry(RegionRole::Safehouse as i32)
        .or_default()
        .entry("grass_company_safehouse_01".to_string())
        .or_default()
        .push("tamed-grassland-lv10-02".to_string());

    RoutesMsg {
        by_role,
        archipelago_routes: build_archipelago_routes(state),
    }
}

fn build_archipelago_msg(arch_id_or_region: &str, now: f64) -> ArchipelagoMsg {
    let meta = crate::state::lookup_region_template(arch_id_or_region)
        .unwrap_or(&crate::state::KNOWN_REGION_TEMPLATES[5]); // default to unstable-savanna-lv15-01
    ArchipelagoMsg {
        id: meta.zone_code.to_string(),
        template_id: meta.template_id.to_string(),
        unstable_factor: 1,
        name: crate::state::region_display_name(meta.zone_code),
        expires_at: now + 7.0 * 86_400.0,
        included_region_ids: vec![meta.zone_code.to_string()],
    }
}

/// Ensures every free (`skill_point == 0`) starter skill node whose `category_level` the player
/// already meets is learned in SQLite. Costs 0 SP and unlocks starter crafting recipes & blueprints.
async fn ensure_starter_skills(state: &ServerState, entity_id: &str) {
    let free_nodes = state.catalog.free_skill_nodes();
    if free_nodes.is_empty() {
        return;
    }
    let stored_cats = state.db.list_skill_categories(entity_id).unwrap_or_default();
    let existing = state.db.list_player_skills(entity_id).unwrap_or_default();
    let mut current_levels: HashMap<(String, String), i32> = HashMap::new();
    for s in existing {
        current_levels.insert((s.skill_id, s.sub_id), s.level);
    }
    for node in free_nodes {
        let cat_lv = stored_cats
            .get(&node.category)
            .map(|(lv, _)| *lv)
            .unwrap_or(1);
        if node.category_level > cat_lv {
            continue;
        }
        let key = (node.skill_id.clone(), node.sub_id.clone());
        let cur = current_levels.get(&key).copied().unwrap_or(0);
        if node.level > cur {
            let _ = state.db.upsert_player_skill(
                entity_id,
                node.category,
                &node.skill_id,
                &node.sub_id,
                node.level,
            );
            current_levels.insert(key, node.level);
        }
    }
}

fn build_recipes_and_blueprints(
    state: &ServerState,
    entity_id: &str,
) -> (RecipesMsg, ArtifactBlueprintsMsg) {
    let learned: Vec<(String, String, i32)> = state
        .db
        .list_player_skills(entity_id)
        .unwrap_or_default()
        .into_iter()
        .map(|s| (s.skill_id, s.sub_id, s.level))
        .collect();
    let recipes = RecipesMsg {
        ids: state.catalog.unlocked_recipe_ids(&learned),
        liked_recipe_ids: Vec::new(),
        new_recipe_ids: Vec::new(),
    };
    let blueprints = ArtifactBlueprintsMsg {
        ids: state.catalog.unlocked_blueprint_ids(&learned),
        liked_blueprint_ids: Vec::new(),
        new_blueprint_ids: Vec::new(),
    };
    (recipes, blueprints)
}

async fn push_recipes_and_blueprints(
    state: &ServerState,
    tx: &mpsc::Sender<Vec<u8>>,
    next_seq: &AtomicU32,
    entity_id: &str,
    now: f64,
) {
    let (recipes, blueprints) = build_recipes_and_blueprints(state, entity_id);
    if let Ok(packed) = recipes.pack() {
        if let Ok(frame) = encode_frame(
            now,
            next_seq.fetch_add(1, Ordering::Relaxed),
            0,
            TC_RECIPES,
            &packed,
        ) {
            let _ = tx.send(frame).await;
        }
    }
    if let Ok(packed) = blueprints.pack() {
        if let Ok(frame) = encode_frame(
            now,
            next_seq.fetch_add(1, Ordering::Relaxed),
            0,
            TC_ARTIFACT_BLUEPRINTS,
            &packed,
        ) {
            let _ = tx.send(frame).await;
        }
    }
}

fn estate_record_to_msg(rec: &EstateRecord) -> EstateLicenseMsg {
    EstateLicenseMsg {
        estate_id: rec.estate_id.clone(),
        owner_type: rec.owner_type,
        owner_id: rec.owner_id.clone(),
        activated_at: rec.activated_at,
        expires_at: if rec.expires_at > 0.0 {
            Some(rec.expires_at)
        } else {
            None
        },
        access_for_others: rec.access_for_others,
        size: (rec.cells.len() as i32).max(1),
        region_id: rec.region_id.clone(),
        tile_x: rec.anchor_x,
        tile_y: rec.anchor_y,
    }
}

fn build_estate_licenses_msg(state: &ServerState, entity_id: &str) -> EstateLicensesMsg {
    let mut estates = state.db.list_estates_by_owner(entity_id).unwrap_or_default();
    if let Ok(Some((clan_id, _, _))) = state.db.get_player_clan(entity_id) {
        for ce in state.db.list_estates_by_owner(&clan_id).unwrap_or_default() {
            if ce.owner_type == OWNER_TYPE_CLAN_ESTATE
                && !estates.iter().any(|e| e.estate_id == ce.estate_id)
            {
                estates.push(ce);
            }
        }
    }
    let mut msg = EstateLicensesMsg::default();
    for rec in &estates {
        let lic = estate_record_to_msg(rec);
        let sz = lic.size;
        match rec.owner_type {
            OWNER_TYPE_PERSONAL_PLAYER => {
                msg.largest_personal_estate_size = msg.largest_personal_estate_size.max(sz);
                msg.personal_estate = Some(lic);
            }
            OWNER_TYPE_CLAN_ESTATE => {
                msg.largest_clan_estate_size = msg.largest_clan_estate_size.max(sz);
                msg.clan_estate = Some(lic);
            }
            _ => {
                msg.largest_urban_estate_size = msg.largest_urban_estate_size.max(sz);
                msg.urban_estate = Some(lic);
            }
        }
    }
    msg
}

fn ensure_personal_region_points(
    state: &ServerState,
    entity_id: &str,
    fallback_region: &str,
) -> crate::db::PlayerPointsRecord {
    let mut pts = state.db.get_player_points(entity_id).unwrap_or_default();
    if pts.personal_region_id.is_none() {
        let pr_code = if fallback_region.starts_with("tamed-") || fallback_region.starts_with("pe") {
            crate::state::format_region_code(fallback_region)
        } else {
            crate::state::DEFAULT_REGION_CODE.to_string()
        };
        let tpl_id = crate::state::terrain_id_for_region(&pr_code);
        pts.personal_region_id = Some(pr_code);
        pts.personal_region_template_id = Some(tpl_id);
        let _ = state.db.upsert_player_points(entity_id, &pts);
    }
    pts
}

async fn build_personal_region_info_msg(
    state: &ServerState,
    entity_id: &str,
    fallback_region: &str,
) -> PersonalRegionInfoMsg {
    let pts = ensure_personal_region_points(state, entity_id, fallback_region);
    let pr_id = pts
        .personal_region_id
        .clone()
        .unwrap_or_else(|| crate::state::DEFAULT_REGION_CODE.to_string());
    let tpl_id = pts
        .personal_region_template_id
        .clone()
        .unwrap_or_else(|| crate::state::terrain_id_for_region(&pr_id));
    let mut region = build_region_msg(state, &tpl_id).await;
    region.id = pr_id;
    region.role = RegionRole::Personal;
    let personal_estate = build_estate_licenses_msg(state, entity_id).personal_estate;
    let pioneer_exp = state
        .resolve_character(entity_id)
        .await
        .map_or(0, |c| c.exp.max(0));
    PersonalRegionInfoMsg {
        personal_region: Some(PersonalRegionMsg {
            region,
            owner_id: entity_id.to_string(),
            pioneer_exp,
            admission_categories: pts.admission_categories,
        }),
        personal_estate,
    }
}

async fn build_points_msg(
    state: &ServerState,
    entity_id: &str,
    current_region_id: &str,
) -> PointsMsg {
    let pts = state.db.get_player_points(entity_id).unwrap_or_default();
    let cur_region = build_region_msg(state, current_region_id).await;
    let home_art = pts
        .home_artifact_id
        .as_deref()
        .and_then(|aid| state.db.get_artifact(aid).ok().flatten());
    let home_point = match home_art {
        Some(a) => {
            let home_region = if a.region_id == current_region_id {
                cur_region.clone()
            } else {
                build_region_msg(state, &a.region_id).await
            };
            let name = state
                .catalog
                .resolve_artifact_type(a.entity_type)
                .map(|s| s.display_name.clone())
                .unwrap_or_else(|| a.blueprint_id.clone());
            Some(PointEntityTileMsg {
                region: home_region,
                tile_x: a.tile_x,
                tile_y: a.tile_y,
                entity_id: a.entity_id,
                entity_name: name,
            })
        }
        None => None,
    };
    let ret_region_id = pts
        .returning_region_id
        .as_deref()
        .filter(|s| !s.is_empty())
        .unwrap_or(current_region_id);
    let ret_terrain = state.terrains.get_or_load(ret_region_id).await;
    let ret_region = if ret_region_id == current_region_id {
        cur_region
    } else {
        build_region_msg(state, ret_region_id).await
    };
    let (ret_x, ret_y) = match (pts.returning_x, pts.returning_y) {
        (Some(x), Some(y)) => (x, y),
        _ => ret_terrain
            .pois
            .port_points
            .first()
            .copied()
            .unwrap_or(ret_terrain.entry_tile),
    };
    PointsMsg {
        home_point,
        returning_point: RegionTileMsg {
            region: ret_region,
            tile_x: ret_x,
            tile_y: ret_y,
        },
    }
}

fn build_estate_grids_for_region(
    state: &ServerState,
    region_id: &str,
    chunks: Vec<(u8, u8)>,
) -> Option<EstateGridsMsg> {
    let estates = state.db.list_estates_in_region(region_id).unwrap_or_default();
    if estates.is_empty() {
        return None;
    }
    let mut cells = Vec::new();
    let mut licenses = Vec::with_capacity(estates.len());
    for rec in &estates {
        for &c in &rec.cells {
            cells.push((c, rec.estate_id.clone()));
        }
        licenses.push(estate_record_to_msg(rec));
    }
    Some(EstateGridsMsg {
        chunks,
        cells,
        estate_licenses: licenses,
    })
}

fn nearest_point(candidates: &[(u16, u16)], from_x: f32, from_y: f32) -> Option<(u16, u16)> {
    let px = (from_x / 200.0).round() as i64;
    let py = (from_y / 200.0).round() as i64;
    candidates
        .iter()
        .copied()
        .min_by_key(|&(tx, ty)| {
            let dx = i64::from(tx) - px;
            let dy = i64::from(ty) - py;
            dx * dx + dy * dy
        })
}

/// Canonical Durango warp sequence (confirmed in `Player.Warp.cs`):
/// 1) Send `Timer (1134)` as the FIRST and ONLY reply at `reply_of = req_seq` (no OK/Mark before it!).
/// 2) After `duration_secs`, update player position in LiveClient + SQLite and push `Teleported (2037)`
///    at `reply_of = 0` with `Tile` in tile units.
async fn start_warp_sequence(
    state: ServerState,
    tx: mpsc::Sender<Vec<u8>>,
    next_seq: Arc<AtomicU32>,
    entity_id: String,
    conn_id: u64,
    expected_region_id: String,
    req_seq: u32,
    dest_tile: (u16, u16),
    teleport_type: i32,
    duration_secs: f32,
) -> Result<(), durango_wire::WireError> {
    let (updated, target_region) = {
        let mut live = state.live_clients.write().await;
        match live.get_mut(&entity_id) {
            Some(c) if c.conn_id == conn_id && c.is_alive => {
                c.warp_generation = c.warp_generation.wrapping_add(1);
                c.stop_rest(ServerState::now_unix_secs());
                (Some(c.warp_generation), c.region_id.clone())
            }
            _ => (None, String::new()),
        }
    };
    let Some(expected_gen) = updated else {
        let now = ServerState::now_unix_secs();
        let abort = AbortMsg {
            text: "Cannot teleport right now.".into(),
        };
        let bytes = encode_frame(
            now,
            next_seq.fetch_add(1, Ordering::Relaxed),
            req_seq,
            TC_ABORT,
            &abort.pack()?,
        )?;
        let _ = tx.send(bytes).await;
        return Ok(());
    };
    let _ = target_region;
    let now = ServerState::now_unix_secs();
    let timer_frame = encode_frame(
        now,
        next_seq.fetch_add(1, Ordering::Relaxed),
        req_seq,
        TC_TIMER,
        &TimerMsg {
            duration: duration_secs,
        }
        .pack()?,
    )?;
    let _ = tx.send(timer_frame).await;

    let delay_ms = (duration_secs.max(0.05) * 1000.0).round() as u64;
    tokio::spawn(async move {
        tokio::time::sleep(Duration::from_millis(delay_ms)).await;
        let done_now = ServerState::now_unix_secs();
        let world_x = f32::from(dest_tile.0) * 200.0;
        let world_y = f32::from(dest_tile.1) * 200.0;
        let updated = {
            let mut live = state.live_clients.write().await;
            match live.get_mut(&entity_id) {
                Some(client)
                    if client.conn_id == conn_id
                        && client.is_alive
                        && client.region_id == expected_region_id
                        && client.warp_generation == expected_gen =>
                {
                    client.pos_x = world_x;
                    client.pos_y = world_y;
                    client.in_battle = false;
                    client.battle_target_id = None;
                    true
                }
                _ => false,
            }
        };
        if !updated {
            return;
        }
        if let Some(tmp) = state.temp_characters.write().await.get_mut(&entity_id) {
            tmp.pos_x = world_x;
            tmp.pos_y = world_y;
        }
        let _ = state.db.update_position(&entity_id, world_x, world_y, done_now);

        if let Ok(packed) = (TeleportedMsg {
            tile_x: dest_tile.0,
            tile_y: dest_tile.1,
            teleport_type,
        })
        .pack()
        {
            if let Ok(tp_frame) = encode_frame(
                done_now,
                next_seq.fetch_add(1, Ordering::Relaxed),
                0,
                TC_TELEPORTED,
                &packed,
            ) {
                let _ = tx.send(tp_frame).await;
            }
        }
        // Broadcast updated position to peers in the region
        let mv = MoveMsg {
            entity_id: entity_id.clone(),
            movements: vec![Movement {
                motion_name: "Barehand_Stand".into(),
                motion_option: MOTION_OPT_LOOPING,
                playback_rate: 1.0,
                rot_speed: 540.0,
                path: vec![Location {
                    x: world_x,
                    y: world_y,
                    yaw: 0.0,
                    time: done_now,
                    floor: 0,
                    height: 0.0,
                }],
            }],
        };
        if let Ok(packed) = mv.pack() {
            if let Ok(mf) = encode_frame(
                done_now,
                next_seq.fetch_add(1, Ordering::Relaxed),
                0,
                TC_MOVE,
                &packed,
            ) {
                state
                    .broadcast_region_except(&expected_region_id, &entity_id, &mf)
                    .await;
            }
        }
    });
    Ok(())
}

/// Builds the player's `SkillsMsg` plus their authoritative remaining SP (`total_sp - spent_sp`).
/// Note: Durango's wire `SkillsMsg.skill_point` is **Total/Max SP** — the Unity client computes
/// `RemainSkillPoint = msg.SkillPoint - sum(learned.UsedSp())` itself!
async fn build_skills_state(state: &ServerState, entity_id: &str) -> (SkillsMsg, i32) {
    let ch = state.resolve_character(entity_id).await;
    let char_level = ch.as_ref().map(|c| c.level.max(1)).unwrap_or(1);
    let untrained_count = ch.as_ref().map(|c| c.untrained_count.max(0)).unwrap_or(0);
    let learned = state.db.list_player_skills(entity_id).unwrap_or_default();
    let stored_cats = state.db.list_skill_categories(entity_id).unwrap_or_default();

    let mut categories = BTreeMap::new();
    for cat in ALL_SKILL_CATEGORIES {
        let (lv, xp) = stored_cats.get(&cat).copied().unwrap_or((1, 0));
        categories.insert(
            cat,
            SkillCategoryMsg {
                level: lv.max(1),
                exp: xp.max(0),
            },
        );
    }

    let mut bundles: BTreeMap<(i32, String), BTreeMap<String, i32>> = BTreeMap::new();
    let mut spent_sp: i32 = 0;
    for s in &learned {
        for lv in 1..=s.level {
            let spec = state.catalog.resolve_skill_node(&s.skill_id, &s.sub_id, lv);
            spent_sp = spent_sp.saturating_add(spec.skill_point.max(0));
        }
        bundles
            .entry((s.category, s.skill_id.clone()))
            .or_default()
            .insert(s.sub_id.clone(), s.level);
    }
    let skill_list = bundles
        .into_iter()
        .map(|((category, skill_id), levels)| SkillBundleMsg {
            category,
            skill_id,
            levels,
        })
        .collect();

    let total_sp = INITIAL_SKILL_POINTS
        .saturating_add((char_level - 1).saturating_mul(SKILL_POINTS_PER_LEVEL));
    let remaining_sp = (total_sp - spent_sp).max(0);

    (
        SkillsMsg {
            skill_list,
            skill_point: total_sp,
            categories,
            untrained_count,
        },
        remaining_sp,
    )
}

async fn build_skills_msg(state: &ServerState, entity_id: &str) -> SkillsMsg {
    build_skills_state(state, entity_id).await.0
}

async fn award_xp(
    state: &ServerState,
    tx: &mpsc::Sender<Vec<u8>>,
    next_seq: &AtomicU32,
    entity_id: &str,
    char_xp: i32,
    cat_xp: Option<(i32, i32)>,
) {
    if char_xp <= 0 && cat_xp.is_none() {
        return;
    }
    let now = ServerState::now_unix_secs();
    let Some(ch) = state.resolve_character(entity_id).await else {
        return;
    };
    let old_level = ch.level.max(1);
    let floor_exp = state.catalog.exp_for_level(old_level);
    let base_exp = ch.exp.max(floor_exp);
    let granted_xp = if char_xp > 0 {
        let weight = (char_xp / 10).clamp(1, 5);
        state.catalog.exp_per_action(old_level, weight)
    } else {
        0
    };
    let is_rested = state
        .live_clients
        .read()
        .await
        .get(entity_id)
        .is_some_and(|c| c.well_rested_until > now);
    let bonus_xp = if is_rested && granted_xp > 0 {
        ((granted_xp as f32) * 0.25).round().max(1.0) as i32
    } else {
        0
    };
    let total_xp = granted_xp.saturating_add(bonus_xp);
    let new_exp = base_exp
        .saturating_add(total_xp)
        .min(state.catalog.exp_cap());
    let new_level = old_level
        .max(state.catalog.level_from_exp(new_exp))
        .min(60);
    let _ = state.db.update_character_level_exp(entity_id, new_level, new_exp);
    if let Some(tmp) = state.temp_characters.write().await.get_mut(entity_id) {
        tmp.level = new_level;
        tmp.exp = new_exp;
    }

    if new_level != old_level {
        if let Some(c) = state.live_clients.write().await.get_mut(entity_id) {
            c.level = new_level;
        }
    }

    if let Some((cat, dxp)) = cat_xp {
        if dxp > 0 {
            let stored = state.db.list_skill_categories(entity_id).unwrap_or_default();
            let (cur_lv, cur_xp) = stored.get(&cat).copied().unwrap_or((1, 0));
            let next_cat_xp = cur_xp.max(0).saturating_add(dxp);
            let next_cat_lv = cur_lv
                .max(cat_level_from_exp(next_cat_xp))
                .min(new_level)
                .min(60);
            let _ = state
                .db
                .upsert_skill_category(entity_id, cat, next_cat_lv, next_cat_xp);
            if next_cat_lv > cur_lv {
                ensure_starter_skills(state, entity_id).await;
            }
        }
    }

    if total_xp > 0 {
        let eg = ExpGainedMsg {
            entity_id: entity_id.to_string(),
            exp: total_xp,
            bonus_exp: bonus_xp,
            resistance_exp: 0,
        };
        if let Ok(packed) = eg.pack() {
            if let Ok(frame) = encode_frame(
                now,
                next_seq.fetch_add(1, Ordering::Relaxed),
                0,
                TC_EXP_GAINED,
                &packed,
            ) {
                let _ = tx.send(frame).await;
            }
        }
    }

    let stats = StatisticsMsg {
        level: new_level,
        exp: new_exp,
    };
    if let Ok(packed) = stats.pack() {
        if let Ok(frame) = encode_frame(
            now,
            next_seq.fetch_add(1, Ordering::Relaxed),
            0,
            TC_STATISTICS,
            &packed,
        ) {
            let _ = tx.send(frame).await;
        }
    }

    let skills = build_skills_msg(state, entity_id).await;
    if let Ok(packed) = skills.pack() {
        if let Ok(frame) = encode_frame(
            now,
            next_seq.fetch_add(1, Ordering::Relaxed),
            0,
            TC_SKILLS,
            &packed,
        ) {
            let _ = tx.send(frame).await;
        }
    }

    if new_level > old_level {
        let (actor_name, actor_ip, actor_reg) = state
            .live_clients
            .read()
            .await
            .get(entity_id)
            .map(|c| (c.name.clone(), c.peer_ip.clone(), c.region_id.clone()))
            .unwrap_or_else(|| (ch.name.clone(), String::new(), ch.region_id.clone()));
        state.db.log_audit(
            now,
            "SKILL",
            &actor_name,
            &actor_ip,
            &actor_reg,
            &format!("Leveled up from Lv.{old_level} to Lv.{new_level}"),
        );
        let rewarded = RewardedLevelUpMsg { level: new_level };
        if let Ok(packed) = rewarded.pack() {
            if let Ok(frame) = encode_frame(
                now,
                next_seq.fetch_add(1, Ordering::Relaxed),
                0,
                TC_REWARDED,
                &packed,
            ) {
                let _ = tx.send(frame).await;
            }
        }
    }
}

pub async fn run_tcp_server(listener: TcpListener, state: ServerState) -> std::io::Result<()> {
    loop {
        let (stream, peer) = listener.accept().await?;
        let st = state.clone();
        tokio::spawn(async move {
            if let Err(err) = handle_connection(stream, peer, st).await {
                tracing::debug!(%peer, %err, "tcp connection ended");
            }
        });
    }
}

async fn handle_connection(
    stream: TcpStream,
    peer: SocketAddr,
    state: ServerState,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let _ = stream.set_nodelay(true);
    let (mut reader, mut writer) = stream.into_split();
    let (tx, mut rx) = mpsc::channel::<Vec<u8>>(256);

    let writer_task = tokio::spawn(async move {
        while let Some(frame) = rx.recv().await {
            if writer.write_all(&frame).await.is_err() {
                break;
            }
        }
        let _ = writer.shutdown().await;
    });

    let conn_id = NEXT_CONN_ID.fetch_add(1, Ordering::Relaxed);
    let peer_ip = peer.ip().to_string();
    let (kick_tx, mut kick_rx) = tokio::sync::watch::channel(false);
    let mut read_buf = Vec::<u8>::with_capacity(8192);
    let mut chunk = [0u8; 4096];
    let next_seq = Arc::new(AtomicU32::new(1));
    let active_gather_gen = Arc::new(AtomicU64::new(0));
    let mut active_gather_target: Option<(f32, f32)> = None;
    let mut active_gather_cancel: Option<oneshot::Sender<()>> = None;
    let active_craft_gen = Arc::new(AtomicU64::new(0));
    let mut active_craft_cancel: Option<oneshot::Sender<()>> = None;
    let mut authed_entity: Option<String> = None;
    let mut authed_owner = String::new();
    let mut authed_name = String::new();
    let mut authed_region: Option<String> = None;
    let mut is_ready = false;
    let mut emigrated = false;
    let mut touched_naturals: HashMap<(u16, u16), u16> = HashMap::new();
    let mut loaded_chunks: HashSet<(u8, u8)> = HashSet::new();
    let mut visible_artifacts: HashSet<String> = HashSet::new();
    let mut last_poi_searched_at: f64 = 0.0;

    let loop_res: Result<(), Box<dyn std::error::Error + Send + Sync>> = async {
        'conn: loop {
        let n = tokio::select! {
            res = reader.read(&mut chunk) => match res {
                Ok(0) | Err(_) => break 'conn,
                Ok(n) => n,
            },
            _ = kick_rx.changed() => {
                if *kick_rx.borrow() {
                    let now = ServerState::now_unix_secs();
                    let abort = AbortMsg {
                        text: "Disconnected by server administrator.".into(),
                    };
                    if let Ok(payload) = abort.pack() {
                        if let Ok(bytes) = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_ABORT,
                            &payload,
                        ) {
                            let _ = tx.send(bytes).await;
                        }
                    }
                    break 'conn;
                }
                continue;
            }
        };
        read_buf.extend_from_slice(&chunk[..n]);

        loop {
            let Some((frame, consumed)) = try_decode_frame(&read_buf)? else {
                break;
            };
            read_buf.drain(..consumed);
            let h = frame.header;

            match h.type_code {
                TC_GET_CLOCK => {
                    let gc = GetClock::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let resp = Clock {
                        client_time: gc.time,
                        server_time: now,
                    };
                    let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                    let bytes = encode_frame(now, seq, h.seq, TC_CLOCK, &resp.pack()?)?;
                    let _ = tx.send(bytes).await;
                }
                TC_AUTH => {
                    if authed_entity.is_some() {
                        tracing::warn!(%peer, "TCP Auth rejected: duplicate TC_AUTH on active connection");
                        let now = ServerState::now_unix_secs();
                        let abort = AbortMsg {
                            text: "Already authenticated.".into(),
                        };
                        let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                        let bytes = encode_frame(now, seq, h.seq, TC_ABORT, &abort.pack()?)?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let auth = Auth::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let session_entry = state
                        .get_valid_session(&auth.session_token)
                        .await
                        .filter(|s| s.entity_id == auth.entity_id);
                    let Some(session_entry) = session_entry else {
                        tracing::warn!(%peer, entity = %auth.entity_id, "TCP Auth rejected: invalid session token");
                        let abort = AbortMsg {
                            text: "Authentication failed: invalid session token.".into(),
                        };
                        let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                        let bytes = encode_frame(now, seq, h.seq, TC_ABORT, &abort.pack()?)?;
                        let _ = tx.send(bytes).await;
                        break 'conn;
                    };
                    if state.effective_join_password().is_some() && !session_entry.password_ok {
                        tracing::warn!(%peer, entity = %auth.entity_id, "TCP Auth rejected: server join password required");
                        let abort = AbortMsg {
                            text: "This server requires a join password.".into(),
                        };
                        let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                        let bytes = encode_frame(now, seq, h.seq, TC_ABORT, &abort.pack()?)?;
                        let _ = tx.send(bytes).await;
                        break 'conn;
                    }
                    let Some(ch) = state.resolve_character(&auth.entity_id).await else {
                        let abort = AbortMsg {
                            text: "Character not found.".into(),
                        };
                        let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                        let bytes = encode_frame(now, seq, h.seq, TC_ABORT, &abort.pack()?)?;
                        let _ = tx.send(bytes).await;
                        break 'conn;
                    };
                    if let Some(ban) = state.db.check_ban(
                        Some(&session_entry.owner_key),
                        Some(&ch.entity_id),
                        Some(&ch.name),
                        Some(&peer_ip),
                        now,
                    ) {
                        tracing::warn!(
                            %peer,
                            entity = %ch.entity_id,
                            ban_type = %ban.ban_type,
                            reason = %ban.reason,
                            "TCP Auth rejected: banned"
                        );
                        let msg = if ban.reason.is_empty() {
                            "You are banned from this community server.".to_string()
                        } else {
                            format!("You are banned from this server: {}", ban.reason)
                        };
                        let abort = AbortMsg { text: msg };
                        let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                        let bytes = encode_frame(now, seq, h.seq, TC_ABORT, &abort.pack()?)?;
                        let _ = tx.send(bytes).await;
                        break 'conn;
                    }
                    authed_owner = session_entry.owner_key.clone();
                    let _ = state.db.update_character_last_ip(&ch.entity_id, &peer_ip);

                    let terrain = state.terrains.get_or_load(&ch.terrain_id).await;
                    let pts = ensure_personal_region_points(&state, &ch.entity_id, &ch.region_id);
                    let storage = state
                        .db
                        .list_player_storage(&ch.entity_id)
                        .unwrap_or_default();
                    let storage_keys = storage.len();
                    let welcome = Welcome {
                        user_id: ch.entity_id.clone(),
                        name: ch.name.clone(),
                        region: RegionMsg {
                            id: ch.region_id.clone(),
                            terrain_id: terrain.id.clone(),
                            template_id: terrain.template_id.clone(),
                            role: region_role_for(&ch.region_id),
                            name: None,
                            created_at: 0.0,
                        },
                        storage,
                        bool_options: vec![("market.ui_enabled".into(), true)],
                        int_options: vec![("market.search.limit".into(), 20)],
                        personal_region_id: pts.personal_region_id,
                        engagement_reward_sent: false,
                    };
                    let seq = next_seq.fetch_add(1, Ordering::Relaxed);
                    let bytes = encode_frame(now, seq, h.seq, TC_WELCOME, &welcome.pack()?)?;
                    let _ = tx.send(bytes).await;

                    authed_entity = Some(ch.entity_id.clone());
                    authed_name = ch.name.clone();
                    authed_region = Some(ch.region_id.clone());
                    tracing::info!(
                        %peer,
                        entity = %ch.entity_id,
                        name = %ch.name,
                        region = %ch.region_id,
                        storage_keys,
                        "TCP Auth -> Welcome sent"
                    );
                }
                TC_READY => {
                    let Some(eid) = authed_entity.clone() else {
                        break 'conn;
                    };
                    let kick_now = ServerState::now_unix_secs();
                    // Flush & kick any prior live session FIRST so resolve_character(&eid)
                    // loads its latest world-unit coordinates and vitals!
                    let kicked_prev = {
                        let mut live = state.live_clients.write().await;
                        live.remove(&eid)
                    };
                    if let Some(mut prev) = kicked_prev {
                        prev.stop_rest(kick_now);
                        let _ = state.db.flush_character_vitals(
                            &prev.entity_id,
                            prev.pos_x,
                            prev.pos_y,
                            prev.life,
                            prev.current_stamina(kick_now),
                            prev.energy,
                            prev.fatigue,
                            kick_now,
                        );
                        if let Some(prev_kick) = prev.kick_tx.take() {
                            let _ = prev_kick.send(true);
                        }
                    }

                    let Some(ch) = state.resolve_character(&eid).await else {
                        break 'conn;
                    };
                    if state.db.get_by_entity_id(&eid).ok().flatten().is_none() {
                        let _ = state.db.upsert_character(&ch);
                        state.temp_characters.write().await.remove(&eid);
                    }
                    let terrain = state.terrains.get_or_load(&ch.terrain_id).await;
                    let (spawn_x, spawn_y) = if ch.pos_x > 200.0 && ch.pos_y > 200.0 {
                        (ch.pos_x, ch.pos_y)
                    } else {
                        let (stx, sty) = terrain.find_nearest_dry_tile(terrain.entry_tile, 32);
                        (f32::from(stx) * 200.0, f32::from(sty) * 200.0)
                    };

                    let now = ServerState::now_unix_secs();
                    let init_life = ch.life.clamp(0.0, 100.0);
                    let init_alive = init_life > 0.0;

                    let (self_clan_id, self_clan_name) = state
                        .db
                        .get_player_clan(&eid)
                        .ok()
                        .flatten()
                        .map(|(cid, cname, _)| (cid, cname))
                        .unwrap_or_default();
                    let spawn_in_balloon =
                        should_spawn_on_air_balloon(&state, &eid, &ch.region_id, init_alive);
                    let init_boarding_on = if spawn_in_balloon {
                        BOARDING_ON_AIR_BALLOON
                    } else {
                        BOARDING_ON_NONE
                    };
                    let init_boarding_since = spawn_in_balloon.then_some(now);
                    let init_boarding_until = spawn_in_balloon.then_some(now + 900.0);

                    let mut self_appearance = state.build_character_appearance(&ch);
                    apply_live_boarding(
                        &mut self_appearance,
                        init_boarding_on,
                        init_boarding_since,
                        init_boarding_until,
                        "",
                    );
                    let self_appear = AppearPlayerMsg {
                        entity_id: eid.clone(),
                        entity_type: self_appearance.entity_type(),
                        is_alive: init_alive,
                        name: ch.name.clone(),
                        level: ch.level.max(1),
                        clan_id: self_clan_id,
                        clan_name: self_clan_name,
                        appearance: self_appearance,
                        pos_x: spawn_x,
                        pos_y: spawn_y,
                        yaw: 0.0,
                        now_secs: now,
                    };
                    let self_appear_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_APPEAR_PLAYER,
                        &self_appear.pack()?,
                    )?;

                    // Snapshot lightweight peer structs inside read lock, then drop lock before SQLite/async!
                    let peer_snapshots: Vec<LiveClient> = {
                        let live = state.live_clients.read().await;
                        live.iter()
                            .filter(|(other_id, other)| {
                                *other_id != &eid && other.region_id == ch.region_id
                            })
                            .map(|(_, other)| other.clone())
                            .collect()
                    };
                    let mut existing_peers: Vec<(LiveClient, Vec<u8>)> =
                        Vec::with_capacity(peer_snapshots.len());
                    for other in peer_snapshots {
                        let (ocid, ocname) = state
                            .db
                            .get_player_clan(&other.entity_id)
                            .ok()
                            .flatten()
                            .map(|(cid, cname, _)| (cid, cname))
                            .unwrap_or_default();
                        let mut other_appearance = state
                            .resolve_character(&other.entity_id)
                            .await
                            .map(|och| state.build_character_appearance(&och))
                            .unwrap_or_default();
                        apply_live_boarding(
                            &mut other_appearance,
                            other.boarding_on,
                            other.boarding_since,
                            other.boarding_until,
                            &other.vehicle_entity_id,
                        );
                        let peer_appear = AppearPlayerMsg {
                            entity_id: other.entity_id.clone(),
                            entity_type: other_appearance.entity_type(),
                            is_alive: other.is_alive,
                            name: other.name.clone(),
                            level: other.level.max(1),
                            clan_id: ocid,
                            clan_name: ocname,
                            appearance: other_appearance,
                            pos_x: other.pos_x,
                            pos_y: other.pos_y,
                            yaw: 0.0,
                            now_secs: now,
                        };
                        if let Ok(packed) = peer_appear.pack() {
                            if let Ok(frame) = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_APPEAR_PLAYER,
                                &packed,
                            ) {
                                existing_peers.push((other, frame));
                            }
                        }
                    }

                    let init_client = LiveClient {
                        conn_id,
                        entity_id: eid.clone(),
                        owner_key: authed_owner.clone(),
                        peer_ip: peer_ip.clone(),
                        name: ch.name.clone(),
                        level: ch.level.max(1),
                        region_id: ch.region_id.clone(),
                        pos_x: spawn_x,
                        pos_y: spawn_y,
                        life: init_life,
                        max_life: 100.0,
                        stamina: ch.stamina.clamp(0.0, 100.0),
                        stamina_at: now,
                        energy: ch.energy.clamp(0.0, 100.0),
                        fatigue: ch.fatigue.clamp(0.0, 100.0),
                        is_alive: init_alive,
                        is_resting: false,
                        rest_updated_at: 0.0,
                        last_dodge_at: 0.0,
                        last_battle_action_at: 0.0,
                        well_rested_until: 0.0,
                        warp_generation: 0,
                        in_battle: false,
                        battle_target_id: None,
                        boarding_on: init_boarding_on,
                        boarding_since: init_boarding_since,
                        boarding_until: init_boarding_until,
                        vehicle_entity_id: String::new(),
                        tx: tx.clone(),
                        kick_tx: Some(kick_tx.clone()),
                    };
                    let init_surv_msg = init_client.to_survival_msg(now);
                    let race_prev = {
                        let mut live = state.live_clients.write().await;
                        live.insert(eid.clone(), init_client)
                    };
                    if let Some(prev) = race_prev {
                        if let Some(prev_kick) = prev.kick_tx {
                            let _ = prev_kick.send(true);
                        }
                    }
                    is_ready = true;

                    // 1. Reply OK to Ready
                    let ok_bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok_bytes).await;

                    // 1a. Initial Survival gauges (loaded from SQLite so HP/energy/fatigue persist)
                    let init_surv_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SURVIVAL,
                        &init_surv_msg.pack()?,
                    )?;
                    let _ = tx.send(init_surv_frame).await;

                    // 1b. Statistics (2040) — auto-migrate pre-cumulative exp up to official floor
                    let char_level = ch.level.max(1);
                    let min_cum_exp = state.catalog.exp_for_level(char_level);
                    let char_exp = if ch.exp < min_cum_exp {
                        let _ = state
                            .db
                            .update_character_level_exp(&eid, char_level, min_cum_exp);
                        if let Some(tmp) = state.temp_characters.write().await.get_mut(&eid) {
                            tmp.exp = min_cum_exp;
                        }
                        min_cum_exp
                    } else {
                        ch.exp.max(0)
                    };
                    let stats = StatisticsMsg {
                        level: char_level,
                        exp: char_exp,
                    };
                    let stats_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_STATISTICS,
                        &stats.pack()?,
                    )?;
                    let _ = tx.send(stats_frame).await;

                    // 1c. Starter 0-SP skills + Skills + Recipes + Blueprints + Points + PioneerGrade + Estates
                    ensure_starter_skills(&state, &eid).await;
                    let init_skills = build_skills_msg(&state, &eid).await;
                    let sk_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SKILLS,
                        &init_skills.pack()?,
                    )?;
                    let _ = tx.send(sk_frame).await;

                    push_recipes_and_blueprints(&state, &tx, &next_seq, &eid, now).await;

                    let init_points = build_points_msg(&state, &eid, &ch.region_id).await;
                    let pts_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_POINTS,
                        &init_points.pack()?,
                    )?;
                    let _ = tx.send(pts_frame).await;

                    let p_grade = ((ch.level.max(1) - 1) / 7 + 1).clamp(1, 9);
                    let pg_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_PIONEER_GRADE_INFO,
                        &PioneerGradeInfoMsg {
                            entity_id: eid.clone(),
                            grade: p_grade,
                            point: 0.0,
                            point_needed: 100 * p_grade,
                            current_maximum_estate_size: 10 + p_grade * 5,
                            current_access_level: p_grade,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(pg_frame).await;

                    let el_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_LICENSES,
                        &build_estate_licenses_msg(&state, &eid).pack()?,
                    )?;
                    let _ = tx.send(el_frame).await;

                    // Push inbox mail on login if the player has any pending mail
                    let init_mails = build_mails_msg(&state, &eid, now);
                    if !init_mails.mails.is_empty() || !init_mails.user_mails.is_empty() {
                        let mf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_MAILS,
                            &init_mails.pack()?,
                        )?;
                        let _ = tx.send(mf).await;
                    }

                    // 2. Inventory (loaded from SQLite)
                    let (inv, _) = build_inventory_msgs(&state, &eid, now);
                    let inv_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY,
                        &inv.pack()?,
                    )?;
                    let _ = tx.send(inv_frame).await;

                    // 3. Equipments (Slot1 preset hydrated from SQLite)
                    let equip = build_equipments_msg(&state, &eid, now);
                    let equip_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_EQUIPMENTS,
                        &equip.pack()?,
                    )?;
                    let _ = tx.send(equip_frame).await;

                    // 4. Defog island chunks
                    let mut all_chunks =
                        Vec::with_capacity(terrain.num_chunks_x() * terrain.num_chunks_y());
                    for cx in 0..terrain.num_chunks_x() {
                        for cy in 0..terrain.num_chunks_y() {
                            all_chunks.push((cx as u8, cy as u8));
                        }
                    }
                    let defog = DefoggedChunks { chunks: all_chunks };
                    let defog_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_DEFOGGED_CHUNKS,
                        &defog.pack()?,
                    )?;
                    let _ = tx.send(defog_frame).await;

                    // 4b. QuestCategories (237902) & CheatFlags (2089)
                    let qc_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_QUEST_CATEGORIES,
                        &pack_empty_quest_categories()?,
                    )?;
                    let _ = tx.send(qc_frame).await;

                    let cf_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_CHEAT_FLAGS,
                        &CheatFlagsMsg {
                            flags: BTreeMap::new(),
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(cf_frame).await;

                    // 5. Send self AppearPlayer + existing peers' AppearPlayer
                    let _ = tx.send(self_appear_frame.clone()).await;
                    for (peer_client, peer_frame) in existing_peers {
                        let _ = tx.send(peer_frame).await;
                        let _ = peer_client.tx.try_send(self_appear_frame.clone());
                    }

                    // 6. Seed island POIs (dock/warphole/crater/rift from pois.yml) & stream all region artifacts
                    state.ensure_region_pois(&ch.region_id, &terrain).await;
                    let region_artifacts = state
                        .db
                        .list_artifacts_in_region(&ch.region_id)
                        .unwrap_or_default();
                    let spawn_cx = ((spawn_x / 200.0).round() as i32) / 16;
                    let spawn_cy = ((spawn_y / 200.0).round() as i32) / 16;
                    let mut init_chunk_window = HashSet::with_capacity(9);
                    for dx in -1..=1 {
                        for dy in -1..=1 {
                            let cx = spawn_cx + dx;
                            let cy = spawn_cy + dy;
                            if cx >= 0 && cy >= 0 {
                                init_chunk_window.insert((cx as u8, cy as u8));
                            }
                        }
                    }
                    visible_artifacts.clear();
                    for art in &region_artifacts {
                        if artifact_overlaps_chunks(art, &init_chunk_window) {
                            visible_artifacts.insert(art.entity_id.clone());
                        }
                        let msg = artifact_record_to_appear_msg(art, &state.catalog, now);
                        if let Ok(packed) = msg.pack() {
                            if let Ok(frame) = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_APPEAR_ARTIFACT,
                                &packed,
                            ) {
                                let _ = tx.send(frame).await;
                            }
                        }
                    }

                    // 7. Seed & stream regional wildlife via AppearAnimal (91)
                    state
                        .ensure_region_wildlife(&ch.region_id, terrain.entry_tile)
                        .await;
                    let region_animals = state.list_animals_in_region(&ch.region_id).await;
                    for animal in &region_animals {
                        let msg = live_animal_to_appear_msg(animal, now);
                        if let Ok(packed) = msg.pack() {
                            if let Ok(frame) = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_APPEAR_ANIMAL,
                                &packed,
                            ) {
                                let _ = tx.send(frame).await;
                            }
                        }
                    }

                    tracing::info!(
                        %peer,
                        entity = %eid,
                        name = %ch.name,
                        spawn_x,
                        spawn_y,
                        items = inv.items.len(),
                        artifacts = region_artifacts.len(),
                        animals = region_animals.len(),
                        "TCP Ready -> OK + Inventory + Equipments + DefoggedChunks + AppearPlayer + AppearArtifact + AppearAnimal synced"
                    );
                    state.db.log_audit(
                        now,
                        "WORLD",
                        &ch.name,
                        &peer_ip,
                        &ch.region_id,
                        &format!(
                            "Entered world (Lv.{} {}, {} items)",
                            ch.level,
                            crate::state::format_account_tag(&authed_owner),
                            inv.items.len()
                        ),
                    );
                }
                TC_SET_CHUNK => {
                    if !is_ready {
                        continue;
                    }
                    let sc = SetChunk::unpack(&frame.msgpack)?;
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    let removed = state.removed_naturals_snapshot(region_id).await;
                    let max_x = terrain.num_chunks_x() as i32;
                    let max_y = terrain.num_chunks_y() as i32;
                    let now = ServerState::now_unix_secs();

                    // Build desired 3x3 chunk window and ONLY stream chunks not already loaded on client
                    // (matching original SetCenterChunks/ClearVisited so already-rendered terrain never re-renders!)
                    let mut desired = HashSet::with_capacity(9);
                    let mut ordered_desired = Vec::with_capacity(9);
                    for dx in -1..=1 {
                        for dy in -1..=1 {
                            let cx = i32::from(sc.x) + dx;
                            let cy = i32::from(sc.y) + dy;
                            if cx >= 0 && cy >= 0 && cx < max_x && cy < max_y {
                                let coord = (cx as u8, cy as u8);
                                desired.insert(coord);
                                ordered_desired.push(coord);
                            }
                        }
                    }
                    loaded_chunks.retain(|c| desired.contains(c));

                    let mut sent_chunks = Vec::new();
                    for (cx, cy) in ordered_desired {
                        if loaded_chunks.insert((cx, cy)) {
                            let msg = ChunkMsg {
                                x: cx,
                                y: cy,
                                garden: terrain.chunk_garden_excluding(cx, cy, &removed),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_CHUNK,
                                &msg.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            sent_chunks.push((cx, cy));
                        }
                    }
                    if !sent_chunks.is_empty() {
                        if let Some(grids) =
                            build_estate_grids_for_region(&state, region_id, sent_chunks.clone())
                        {
                            let gframe = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ESTATE_GRIDS,
                                &grids.pack()?,
                            )?;
                            let _ = tx.send(gframe).await;
                        }
                    }

                    // Re-stream any artifacts entering the player's 3x3 chunk window (mirroring
                    // C# Player.cs:781-808 SetCenterChunks because Unity's ArtifactManager
                    // unloads far artifacts whenever CenterChunkChanged fires!).
                    let region_artifacts = state
                        .db
                        .list_artifacts_in_region(region_id)
                        .unwrap_or_default();
                    let in_window_ids: HashSet<String> = region_artifacts
                        .iter()
                        .filter(|a| artifact_overlaps_chunks(a, &desired))
                        .map(|a| a.entity_id.clone())
                        .collect();
                    visible_artifacts.retain(|id| in_window_ids.contains(id));
                    for art in &region_artifacts {
                        if in_window_ids.contains(&art.entity_id)
                            && visible_artifacts.insert(art.entity_id.clone())
                        {
                            let msg = artifact_record_to_appear_msg(art, &state.catalog, now);
                            if let Ok(packed) = msg.pack() {
                                if let Ok(frame) = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_APPEAR_ARTIFACT,
                                    &packed,
                                ) {
                                    let _ = tx.send(frame).await;
                                }
                            }
                        }
                    }
                    tracing::debug!(chunk_x = sc.x, chunk_y = sc.y, sent = sent_chunks.len(), "TCP SetChunk -> Chunk(s)");
                }
                TC_DEPART => {
                    // Unity fires Depart (2448) on pose/motion transitions as well as walk starts,
                    // so never cancel active gathers here (prevents first-attempt gather cancels).
                    // Real manual walk-away cancellation arrives via TC_CANCEL (2036) from
                    // HudPolishMod's PlayerController.MoveStarted hook or TC_MOVE > 500 units away.
                }
                TC_CANCEL => {
                    if active_gather_cancel.is_some() {
                        tracing::info!(%peer, "TCP Gather cancelled immediately by TC_CANCEL (2036)");
                        active_gather_gen.fetch_add(1, Ordering::Relaxed);
                        active_gather_target = None;
                        if let Some(cancel_tx) = active_gather_cancel.take() {
                            let _ = cancel_tx.send(());
                        }
                    }
                }
                TC_MOVE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let mut mv = MoveMsg::unpack(&frame.msgpack)?;
                    if mv.movements.len() > 16
                        || mv.movements.iter().any(|m| m.path.len() > 64)
                    {
                        tracing::warn!(%peer, entity = %eid, "rejected oversized Move packet");
                        continue;
                    }
                    mv.entity_id = eid.clone();
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    let max_wx = (terrain.width as f32) * 200.0;
                    let max_wy = (terrain.height as f32) * 200.0;
                    if mv.movements.iter().any(|m| {
                        m.path.iter().any(|p| {
                            !p.x.is_finite()
                                || !p.y.is_finite()
                                || p.x < -200.0
                                || p.y < -200.0
                                || p.x > max_wx + 200.0
                                || p.y > max_wy + 200.0
                        })
                    }) {
                        tracing::warn!(%peer, entity = %eid, "rejected out-of-bounds or non-finite Move");
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    if let Some(last_loc) = mv.movements.last().and_then(|m| m.path.last()) {
                        let (is_alive, stopped_rest_surv) = {
                            let mut live = state.live_clients.write().await;
                            if let Some(client) = live.get_mut(&eid) {
                                if !client.is_alive {
                                    (false, None)
                                } else {
                                    client.pos_x = last_loc.x;
                                    client.pos_y = last_loc.y;
                                    let stopped = if client.stop_rest(now) {
                                        Some((
                                            client.to_survival_msg(now),
                                            (
                                                client.life,
                                                client.current_stamina(now),
                                                client.energy,
                                                client.fatigue,
                                            ),
                                        ))
                                    } else {
                                        None
                                    };
                                    (true, stopped)
                                }
                            } else {
                                (true, None)
                            }
                        };
                        if !is_alive {
                            continue;
                        }
                        if let Some((surv, (life, stam, eng, fat))) = stopped_rest_surv {
                            let _ = state.db.update_survival(&eid, life, stam, eng, fat);
                    state.db.log_audit(
                                now,
                                "SURVIVAL",
                                &authed_name,
                                &peer_ip,
                                region_id,
                                "Stopped resting (moved)",
                            );
                            if let Ok(packed) = surv.pack() {
                                if let Ok(sf) = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_SURVIVAL,
                                    &packed,
                                ) {
                                    let _ = tx.send(sf).await;
                                }
                            }
                            send_fatigue_velocity(&tx, &next_seq, now, 0.0).await;
                        }
                        // Safety-net distance check ONLY against the target resource tile (> 2.5 tiles away).
                        // NEVER compare against pre-gather position or motion_name here: Unity batches
                        // MoveToTarget's walk-up Move for 500ms and flushes it AFTER TC_COLLECT starts,
                        // with last_loc right next to the bush! Instant walk-away cancellation is handled
                        // cleanly by TC_DEPART (2448) and TC_CANCEL (2036).
                        if let Some((gx, gy)) = active_gather_target {
                            if (last_loc.x - gx).hypot(last_loc.y - gy) > 500.0 {
                                tracing::info!(%peer, "TCP Gather cancelled by Move >500 units from resource");
                                active_gather_gen.fetch_add(1, Ordering::Relaxed);
                                active_gather_target = None;
                                if let Some(cancel_tx) = active_gather_cancel.take() {
                                    let _ = cancel_tx.send(());
                                }
                            }
                        }
                    }
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let broadcast = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_MOVE,
                        &mv.pack()?,
                    )?;
                    state
                        .broadcast_region_except(region_id, &eid, &broadcast)
                        .await;
                }
                TC_TOUCH => {
                    if !is_ready {
                        continue;
                    }
                    let touch = TouchMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();

                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    // Check wildlife (alive = Attack, slain carcass = Butcher/Collect) first
                    if !touch.entity_id.is_empty() {
                        let animal_opt = state.animals.read().await.get(&touch.entity_id).cloned();
                        if let Some(animal) = animal_opt.filter(|a| a.region_id == region_id) {
                            if !animal.butchered {
                                let touched = if animal.is_alive {
                                    TouchedMsg {
                                        entity_id: animal.entity_id.clone(),
                                        entity_name: animal.name.clone(),
                                        prototype_id: animal.name.clone(),
                                        level: animal.level,
                                        interactions: vec![INTERACTION_ATTACK],
                                        collectible: CollectibleMsg {
                                            entity_id: animal.entity_id,
                                            collectible_id: None,
                                            size: String::new(),
                                            generators: Vec::new(),
                                            critical_generator: String::new(),
                                        },
                                    }
                                } else {
                                    let gens = state
                                        .catalog
                                        .butcher_generators(animal.level)
                                        .into_iter()
                                        .filter_map(|g| {
                                            let rem = animal
                                                .butcher_stock
                                                .get(&g.id)
                                                .copied()
                                                .unwrap_or(g.amount);
                                            if rem <= 0 {
                                                return None;
                                            }
                                            Some(GeneratorMsg {
                                                id: g.id,
                                                level: g.level,
                                                name: g.name,
                                                icon: g.icon,
                                                amount: rem,
                                                effort: g.effort,
                                                duration: g.duration,
                                                tool_requirements: g.tool_requirements,
                                                enabled: true,
                                            })
                                        })
                                        .collect();
                                    TouchedMsg {
                                        entity_id: animal.entity_id.clone(),
                                        entity_name: animal.name.clone(),
                                        prototype_id: animal.name.clone(),
                                        level: animal.level,
                                        interactions: vec![INTERACTION_COLLECT],
                                        collectible: CollectibleMsg {
                                            entity_id: animal.entity_id,
                                            collectible_id: Some(animal.name),
                                            size: "medium".into(),
                                            generators: gens,
                                            critical_generator: String::new(),
                                        },
                                    }
                                };
                                let bytes = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    h.seq,
                                    TC_TOUCHED,
                                    &touched.pack()?,
                                )?;
                                let _ = tx.send(bytes).await;
                                continue;
                            }
                        }
                    }

                    // Check placed world artifacts next (by EntityId)
                    if !touch.entity_id.is_empty() {
                        if let Ok(Some(art)) = state.db.get_artifact(&touch.entity_id).map(|o| o.filter(|a| a.region_id == region_id)) {
                            let spec = state.catalog.resolve_artifact_type(art.entity_type);
                            let bp_lower = art.blueprint_id.to_ascii_lowercase();
                            let is_poi = art.entity_id.starts_with("poi-");
                            let can_destruct = !is_poi
                                && (authed_entity.as_deref() == Some(art.founder_entity_id.as_str())
                                    || state.is_admin_owner(&authed_owner));
                            let is_port = spec.is_some_and(|s| s.is_port)
                                || art.entity_type == 7001
                                || bp_lower == "dock"
                                || art.entity_id.starts_with("poi-port-");
                            let is_warphole = spec.is_some_and(|s| s.is_warphole)
                                || art.entity_type == 9450
                                || bp_lower.contains("warphole")
                                || art.entity_id.starts_with("poi-warphole-");
                            let is_burnable = spec.is_some_and(|s| s.is_burnable)
                                || bp_lower.contains("bonfire")
                                || bp_lower.contains("kiln")
                                || bp_lower.contains("furnace");
                            let is_craft = spec.is_some_and(|s| s.is_craft || !s.workbench_tags.is_empty())
                                || is_burnable
                                || bp_lower.contains("workbench")
                                || bp_lower.contains("table")
                                || bp_lower.contains("kitchen");
                            let can_rest = spec.is_some_and(|s| s.can_rest)
                                || bp_lower.contains("tent")
                                || bp_lower.contains("bed")
                                || bp_lower.contains("chair")
                                || bp_lower.contains("bonfire");
                            let is_home = spec.is_some_and(|s| s.is_home)
                                || bp_lower.contains("tent")
                                || bp_lower.contains("bed");
                            let is_warehouse = spec.is_some_and(|s| s.is_warehouse)
                                || bp_lower.contains("box")
                                || bp_lower.contains("basket")
                                || bp_lower.contains("warehouse");
                            let is_washable = spec.is_some_and(|s| s.is_washable)
                                || bp_lower.contains("well");
                            let is_gate = spec.is_some_and(|s| s.is_gate)
                                || bp_lower.contains("gate");
                            let mut interactions = Vec::new();
                            match art.building_state {
                                BUILDING_STATE_OCCUPIED => {
                                    interactions.push(INTERACTION_BUILD_ARTIFACT);
                                    if can_destruct {
                                        interactions.push(INTERACTION_DESTRUCT_ARTIFACT);
                                    }
                                }
                                BUILDING_STATE_BUILT => {
                                    interactions.push(INTERACTION_COMPLETE_ARTIFACT);
                                    if can_destruct {
                                        interactions.push(INTERACTION_DESTRUCT_ARTIFACT);
                                    }
                                }
                                _ => {
                                    if is_port {
                                        interactions.push(INTERACTION_SAILING_ROUTES);
                                        if region_role_for(&art.region_id) != RegionRole::Personal {
                                            interactions.push(INTERACTION_SAILING_WITHDRAW);
                                        }
                                        interactions.push(INTERACTION_SAILING_RANDOM_PERSONAL);
                                    }
                                    if is_warphole {
                                        interactions.push(INTERACTION_WARP);
                                    }
                                    if is_craft && !is_port && !is_warphole {
                                        interactions.push(INTERACTION_CRAFT);
                                    }
                                    if is_burnable {
                                        interactions.push(INTERACTION_FIRE);
                                        interactions.push(INTERACTION_EXTINGUISH);
                                    }
                                    if can_rest {
                                        interactions.push(INTERACTION_REST);
                                    }
                                    if is_home && !is_poi {
                                        interactions.push(INTERACTION_SET_AS_HOME);
                                    }
                                    if is_warehouse {
                                        interactions.push(INTERACTION_USE_WAREHOUSE);
                                        interactions.push(INTERACTION_INVENTORY);
                                    }
                                    if is_washable {
                                        interactions.push(INTERACTION_WASH);
                                    }
                                    if is_gate {
                                        interactions.push(INTERACTION_OPEN_GATE);
                                        interactions.push(INTERACTION_CLOSE_GATE);
                                    }
                                    if !is_poi && art.durability + 0.5 < art.max_durability {
                                        interactions.push(INTERACTION_REPAIR_ARTIFACT);
                                    }
                                    if can_destruct {
                                        interactions.push(INTERACTION_DESTRUCT_ARTIFACT);
                                    }
                                }
                            }
                            let touched = TouchedMsg {
                                entity_id: art.entity_id.clone(),
                                entity_name: spec
                                    .map(|s| s.display_name.clone())
                                    .unwrap_or_else(|| art.blueprint_id.clone()),
                                prototype_id: art.blueprint_id.clone(),
                                level: i32::from(art.level),
                                interactions,
                                collectible: CollectibleMsg {
                                    entity_id: art.entity_id,
                                    collectible_id: None,
                                    size: String::new(),
                                    generators: Vec::new(),
                                    critical_generator: String::new(),
                                },
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_TOUCHED,
                                &touched.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    }

                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    let removed = state.removed_naturals_snapshot(region_id).await;
                    let tile = (touch.tile_x, touch.tile_y);

                    let resolved_type = if removed.contains(&tile) {
                        None
                    } else {
                        terrain.natural_at(tile.0, tile.1)
                    };

                    if let Some(et) = resolved_type {
                        if touched_naturals.len() > 256 {
                            touched_naturals.clear();
                        }
                        touched_naturals.insert(tile, et);
                        let spec = state.catalog.resolve_natural(et);
                        let stock = state
                            .peek_natural_stock(region_id, tile, &spec)
                            .await;
                        let generators: Vec<GeneratorMsg> = spec
                            .generators
                            .iter()
                            .filter_map(|g| {
                                let rem = stock.get(&g.id).copied().unwrap_or(g.amount);
                                if rem <= 0 {
                                    return None;
                                }
                                Some(GeneratorMsg {
                                    id: g.id.clone(),
                                    level: g.level,
                                    name: g.name.clone(),
                                    icon: g.icon.clone(),
                                    amount: rem,
                                    effort: g.effort,
                                    duration: g.duration,
                                    tool_requirements: g.tool_requirements.clone(),
                                    enabled: true,
                                })
                            })
                            .collect();
                        let touched = TouchedMsg {
                            entity_id: touch.entity_id.clone(),
                            entity_name: spec.display_name,
                            prototype_id: spec.collectible_id.clone(),
                            level: 1,
                            interactions: vec![INTERACTION_COLLECT],
                            collectible: CollectibleMsg {
                                entity_id: touch.entity_id,
                                collectible_id: Some(spec.collectible_id),
                                size: "medium".into(),
                                generators,
                                critical_generator: String::new(),
                            },
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_TOUCHED,
                            &touched.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                    } else {
                        let empty_touched = TouchedMsg {
                            entity_id: touch.entity_id.clone(),
                            entity_name: String::new(),
                            prototype_id: String::new(),
                            level: 1,
                            interactions: Vec::new(),
                            collectible: CollectibleMsg {
                                entity_id: touch.entity_id,
                                collectible_id: None,
                                size: String::new(),
                                generators: Vec::new(),
                                critical_generator: String::new(),
                            },
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_TOUCHED,
                            &empty_touched.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                    }
                }
                TC_COLLECT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let collect = CollectMsg::unpack(&frame.msgpack)?;
                    let region_id = authed_region
                        .clone()
                        .unwrap_or_else(|| crate::state::DEFAULT_REGION_CODE.to_string());
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot gather while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if state.db.list_items(&eid).map_or(0, |v| v.len()) >= MAX_INVENTORY_ITEMS {
                        let abort = AbortMsg {
                            text: "Inventory is full.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    stop_resting_if_active(&state, &tx, &next_seq, &eid, now).await;

                    // Carcass butchering branch: Collect by dead animal EntityId (multi-yield, completion-time decrement!)
                    if !collect.entity_id.is_empty() {
                        let player_pos = state
                            .live_clients
                            .read()
                            .await
                            .get(&eid)
                            .map(|c| (c.pos_x, c.pos_y));
                        let carcass_check: Result<Option<(LiveAnimal, String)>, &'static str> = {
                            let mut animals = state.animals.write().await;
                            if let Some(a) = animals.get_mut(&collect.entity_id) {
                                if a.region_id != region_id {
                                    Err("Target is in another region.")
                                } else if !a.is_alive && !a.butchered {
                                    if let Some((px, py)) = player_pos {
                                        let dist_tiles =
                                            ((px - a.pos_x).hypot(py - a.pos_y)) / 200.0;
                                        if dist_tiles > MAX_COLLECT_RANGE_TILES {
                                            Err("Target is too far to butcher.")
                                        } else {
                                            if a.butcher_stock.is_empty() {
                                                for g in state.catalog.butcher_generators(a.level) {
                                                    a.butcher_stock.insert(g.id, g.amount.max(1));
                                                }
                                            }
                                            let target_key = if collect.generator_id.is_empty() {
                                                a.butcher_stock
                                                    .iter()
                                                    .find(|(_, &v)| v > 0)
                                                    .map(|(k, _)| k.clone())
                                            } else if a
                                                .butcher_stock
                                                .get(&collect.generator_id)
                                                .copied()
                                                .unwrap_or(0)
                                                > 0
                                            {
                                                Some(collect.generator_id.clone())
                                            } else {
                                                None
                                            };
                                            match target_key {
                                                Some(gk) => Ok(Some((a.clone(), gk))),
                                                None => Err("Resource already depleted."),
                                            }
                                        }
                                    } else {
                                        Err("Player not ready.")
                                    }
                                } else {
                                    Ok(None)
                                }
                            } else {
                                Ok(None)
                            }
                        };
                        let carcass = match carcass_check {
                            Ok(opt) => opt,
                            Err(msg) => {
                                let abort = AbortMsg { text: msg.into() };
                                let bytes = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    h.seq,
                                    TC_ABORT,
                                    &abort.pack()?,
                                )?;
                                let _ = tx.send(bytes).await;
                                continue;
                            }
                        };
                        if let Some((animal, chosen_gen_id)) = carcass {
                            if let Some(prev_cancel) = active_gather_cancel.take() {
                                let _ = prev_cancel.send(());
                            }
                            let gens = state.catalog.butcher_generators(animal.level);
                            let chosen = gens
                                .iter()
                                .find(|g| g.id == chosen_gen_id)
                                .or_else(|| gens.first())
                                .cloned();
                            let (proto_id, gen_id, gen_level) = if let Some(g) = chosen {
                                (g.prototype_id, g.id, g.level.max(1))
                            } else {
                                ("meat".into(), "meat".into(), animal.level.max(1))
                            };
                            let (item_name, item_icon, item_size) =
                                state.catalog.prototype_info(&proto_id);

                            let open_mark = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_REPLY_SEQUENCE_MARK,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(open_mark).await;

                            let delay_ms = state.config.collect_delay_ms;
                            let timer_frame = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_TIMER,
                                &TimerMsg {
                                    duration: (delay_ms as f32 / 1000.0).max(0.05),
                                }
                                .pack()?,
                            )?;
                            let _ = tx.send(timer_frame).await;

                            let (cancel_tx, cancel_rx) = oneshot::channel::<()>();
                            active_gather_cancel = Some(cancel_tx);
                            let my_gen = active_gather_gen.fetch_add(1, Ordering::Relaxed) + 1;
                            active_gather_target = Some((animal.pos_x, animal.pos_y));
                            let gather_gen_ref = active_gather_gen.clone();
                            let st_done = state.clone();
                            let tx_done = tx.clone();
                            let seq_done = next_seq.clone();
                            let reply_seq = h.seq;
                            let animal_id = animal.entity_id;
                            let animal_name = animal.name;
                            tokio::spawn(async move {
                                let cancelled = if delay_ms > 0 {
                                    tokio::select! {
                                        _ = tokio::time::sleep(Duration::from_millis(delay_ms)) => false,
                                        _ = cancel_rx => true,
                                    }
                                } else {
                                    false
                                };
                                if cancelled || gather_gen_ref.load(Ordering::Relaxed) != my_gen {
                                    let cnow = ServerState::now_unix_secs();
                                    if let Ok(empty) = pack_empty_struct() {
                                        if let Ok(frame) = encode_frame(
                                            cnow,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            reply_seq,
                                            TC_CANCELED,
                                            &empty,
                                        ) {
                                            let _ = tx_done.send(frame).await;
                                        }
                                        if let Ok(close_mark) = encode_frame(
                                            cnow,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            reply_seq,
                                            TC_REPLY_SEQUENCE_MARK,
                                            &empty,
                                        ) {
                                            let _ = tx_done.send(close_mark).await;
                                        }
                                    }
                                    return;
                                }
                                let done_now = ServerState::now_unix_secs();
                                if !is_entity_alive(&st_done, &eid).await {
                                    if let Ok(empty) = pack_empty_struct() {
                                        if let Ok(frame) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            reply_seq,
                                            TC_CANCELED,
                                            &empty,
                                        ) {
                                            let _ = tx_done.send(frame).await;
                                        }
                                        if let Ok(close_mark) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            reply_seq,
                                            TC_REPLY_SEQUENCE_MARK,
                                            &empty,
                                        ) {
                                            let _ = tx_done.send(close_mark).await;
                                        }
                                    }
                                    return;
                                }
                                let consumed = {
                                    let mut animals = st_done.animals.write().await;
                                    if let Some(a) = animals.get_mut(&animal_id) {
                                        if !a.is_alive && !a.butchered {
                                            if a.butcher_stock.get(&gen_id).copied().unwrap_or(0) > 0
                                            {
                                                if let Some(rem) = a.butcher_stock.get_mut(&gen_id) {
                                                    *rem = (*rem - 1).max(0);
                                                }
                                                let total_left: i32 = a
                                                    .butcher_stock
                                                    .values()
                                                    .copied()
                                                    .filter(|&v| v > 0)
                                                    .sum();
                                                let depleted = total_left <= 0;
                                                if depleted {
                                                    a.butchered = true;
                                                    a.respawn_at = done_now
                                                        + (st_done.config.wildlife_respawn_secs
                                                            * 0.5)
                                                            .max(0.2);
                                                }
                                                Some(depleted)
                                            } else {
                                                None
                                            }
                                        } else {
                                            None
                                        }
                                    } else {
                                        None
                                    }
                                };
                                let Some(carcass_depleted) = consumed else {
                                    if let Ok(packed) = (AbortMsg {
                                        text: "Resource already depleted.".into(),
                                    })
                                    .pack()
                                    {
                                        if let Ok(frame) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            reply_seq,
                                            TC_ABORT,
                                            &packed,
                                        ) {
                                            let _ = tx_done.send(frame).await;
                                        }
                                    }
                                    if let Ok(empty) = pack_empty_struct() {
                                        if let Ok(close_mark) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            reply_seq,
                                            TC_REPLY_SEQUENCE_MARK,
                                            &empty,
                                        ) {
                                            let _ = tx_done.send(close_mark).await;
                                        }
                                    }
                                    return;
                                };
                                let surv_snap = {
                                    let mut live = st_done.live_clients.write().await;
                                    live.get_mut(&eid).map(|c| {
                                        c.apply_gather(done_now);
                                        (
                                            c.to_survival_msg(done_now),
                                            c.life,
                                            c.current_stamina(done_now),
                                            c.energy,
                                            c.fatigue,
                                        )
                                    })
                                };
                                let surv_msg = surv_snap.map(|(m, life, stam, eng, fat)| {
                                    let _ = st_done.db.update_survival(&eid, life, stam, eng, fat);
                                    m
                                });
                                if let Some(sm) = surv_msg {
                                    if let Ok(packed) = sm.pack() {
                                        if let Ok(frame) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            0,
                                            TC_SURVIVAL,
                                            &packed,
                                        ) {
                                            let _ = tx_done.send(frame).await;
                                        }
                                    }
                                }
                                let new_rec = ItemRecord {
                                    item_id: Uuid::new_v4().to_string(),
                                    entity_id: eid.clone(),
                                    prototype_id: proto_id,
                                    name: item_name,
                                    icon: item_icon,
                                    level: gen_level,
                                    size: item_size,
                                    durability: 100.0,
                                    max_durability: 100.0,
                                    locked: false,
                                    collectible_id: Some(animal_name),
                                    generator_id: Some(gen_id),
                                    created_at: done_now,
                                };
                                let _ = st_done.db.insert_items(std::slice::from_ref(&new_rec));
                                let (actor_name, actor_ip) = st_done
                                    .live_clients
                                    .read()
                                    .await
                                    .get(&eid)
                                    .map(|c| (c.name.clone(), c.peer_ip.clone()))
                                    .unwrap_or_else(|| (eid.clone(), String::new()));
                                st_done.db.log_audit(
                                    done_now,
                                    "GATHER",
                                    &actor_name,
                                    &actor_ip,
                                    &region_id,
                                    &format!(
                                        "Butchered {} (Lv.{}) from {}",
                                        new_rec.name,
                                        new_rec.level,
                                        new_rec.collectible_id.as_deref().unwrap_or("carcass")
                                    ),
                                );
                                let item_msg =
                                    item_record_to_msg(&new_rec, &st_done.catalog, done_now);

                                // Unity's InventorySystem adds items to live bag on InventoryUpdated (113)
                                let inv_upd = InventoryUpdatedMsg {
                                    entity_id: eid.clone(),
                                    items: vec![item_msg.clone()],
                                    removed_item_ids: Vec::new(),
                                    item_order: None,
                                };
                                if let Ok(packed) = inv_upd.pack() {
                                    if let Ok(frame) = encode_frame(
                                        done_now,
                                        seq_done.fetch_add(1, Ordering::Relaxed),
                                        0,
                                        TC_INVENTORY_UPDATED,
                                        &packed,
                                    ) {
                                        let _ = tx_done.send(frame).await;
                                    }
                                }

                                let collected = CollectedMsg {
                                    items: vec![item_msg],
                                    result: RESULT_SUCCESS,
                                    action_level: gen_level,
                                    potential_level: gen_level,
                                    success_ratio: 1.0,
                                    ran_out: carcass_depleted,
                                };
                                if let Ok(packed) = collected.pack() {
                                    if let Ok(frame) = encode_frame(
                                        done_now,
                                        seq_done.fetch_add(1, Ordering::Relaxed),
                                        reply_seq,
                                        TC_COLLECTED,
                                        &packed,
                                    ) {
                                        let _ = tx_done.send(frame).await;
                                    }
                                }
                                if let Ok(empty) = pack_empty_struct() {
                                    if let Ok(close_mark) = encode_frame(
                                        done_now,
                                        seq_done.fetch_add(1, Ordering::Relaxed),
                                        reply_seq,
                                        TC_REPLY_SEQUENCE_MARK,
                                        &empty,
                                    ) {
                                        let _ = tx_done.send(close_mark).await;
                                    }
                                }
                                if carcass_depleted {
                                    let dis = DisappearEntity {
                                        entity_id: animal_id,
                                    };
                                    if let Ok(packed) = dis.pack() {
                                        if let Ok(frame) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            0,
                                            TC_DISAPPEAR_ENTITY,
                                            &packed,
                                        ) {
                                            st_done.broadcast_region_all(&region_id, &frame).await;
                                        }
                                    }
                                } else {
                                    let changed = CollectibleChangedMsg {
                                        entity_id: animal_id,
                                    };
                                    if let Ok(packed) = changed.pack() {
                                        if let Ok(frame) = encode_frame(
                                            done_now,
                                            seq_done.fetch_add(1, Ordering::Relaxed),
                                            0,
                                            TC_COLLECTIBLE_CHANGED,
                                            &packed,
                                        ) {
                                            st_done.broadcast_region_all(&region_id, &frame).await;
                                        }
                                    }
                                }
                                award_xp(
                                    &st_done,
                                    &tx_done,
                                    &seq_done,
                                    &eid,
                                    30,
                                    Some((SKILL_CAT_BUTCHERY, 25)),
                                )
                                .await;
                            });
                            continue;
                        }
                    }

                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let tile = (collect.tile_x, collect.tile_y);

                    // Validate player is alive & within range of target tile
                    let player_pos_and_alive = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .map(|c| (c.pos_x, c.pos_y, c.is_alive));
                    if let Some((px, py, is_alive)) = player_pos_and_alive {
                        if !is_alive {
                            let abort = AbortMsg {
                                text: "Cannot gather while unconscious.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                        let dist_tiles =
                            ((px / 200.0) - f32::from(tile.0)).hypot((py / 200.0) - f32::from(tile.1));
                        if dist_tiles > MAX_COLLECT_RANGE_TILES {
                            let abort = AbortMsg {
                                text: "Target is too far to gather.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    }

                    let Some(entity_type) = terrain.natural_at(tile.0, tile.1) else {
                        let abort = AbortMsg {
                            text: "No gatherable natural resource at tile.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };

                    let spec = state.catalog.resolve_natural(entity_type);
                    let removed_now = state.removed_naturals_snapshot(&region_id).await;
                    let stock_now = state
                        .peek_natural_stock(&region_id, tile, &spec)
                        .await;
                    let chosen_gen = if collect.generator_id.is_empty() {
                        spec.generators
                            .iter()
                            .find(|g| stock_now.get(&g.id).copied().unwrap_or(0) > 0)
                            .or_else(|| spec.generators.first())
                            .cloned()
                    } else {
                        spec.generators
                            .iter()
                            .find(|g| g.id == collect.generator_id)
                            .cloned()
                    };

                    let Some(g) = chosen_gen else {
                        let abort = AbortMsg {
                            text: "Resource generator not available.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    let (proto_id, gen_id, gen_level) = (g.prototype_id, g.id, g.level.max(1));

                    // Verify exact requested generator has stock available; actual decrement happens
                    // atomically at timer completion so stacked ReservationQueue gathers show accurate counts
                    // (3 -> 2 -> 1 -> 0) and walking away mid-gather never consumes a unit or energy!
                    let has_stock = !removed_now.contains(&tile)
                        && stock_now.get(&gen_id).copied().unwrap_or(0) > 0;
                    if !has_stock {
                        if stock_now.values().all(|&v| v <= 0) {
                            touched_naturals.remove(&tile);
                        }
                        let abort = AbortMsg {
                            text: "Resource already depleted.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let (item_name, item_icon, item_size) = state.catalog.prototype_info(&proto_id);

                    if let Some(prev_cancel) = active_gather_cancel.take() {
                        let _ = prev_cancel.send(());
                    }

                    // Open reply sequence on h.seq -> Timer
                    let open_mark = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REPLY_SEQUENCE_MARK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(open_mark).await;

                    let delay_ms = state.config.collect_delay_ms;
                    let duration_secs = (delay_ms as f32 / 1000.0).max(0.05);
                    let timer_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_TIMER,
                        &TimerMsg {
                            duration: duration_secs,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(timer_frame).await;

                    // Spawn non-blocking completion so TCP reader stays responsive during gather timer
                    let (cancel_tx, cancel_rx) = oneshot::channel::<()>();
                    active_gather_cancel = Some(cancel_tx);
                    let my_gen = active_gather_gen.fetch_add(1, Ordering::Relaxed) + 1;
                    active_gather_target =
                        Some((f32::from(tile.0) * 200.0, f32::from(tile.1) * 200.0));
                    let gather_gen_ref = active_gather_gen.clone();
                    let st_done = state.clone();
                    let tx_done = tx.clone();
                    let seq_done = next_seq.clone();
                    let reply_seq = h.seq;
                    let entity_id_arg = collect.entity_id.clone();
                    let collectible_id = spec.collectible_id.clone();
                    tokio::spawn(async move {
                        let cancelled = if delay_ms > 0 {
                            tokio::select! {
                                _ = tokio::time::sleep(Duration::from_millis(delay_ms)) => false,
                                _ = cancel_rx => true,
                            }
                        } else {
                            false
                        };
                        if cancelled || gather_gen_ref.load(Ordering::Relaxed) != my_gen {
                            let cnow = ServerState::now_unix_secs();
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(frame) = encode_frame(
                                    cnow,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_CANCELED,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(frame).await;
                                }
                                if let Ok(close_mark) = encode_frame(
                                    cnow,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        }
                        let done_now = ServerState::now_unix_secs();
                        if !is_entity_alive(&st_done, &eid).await {
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(frame) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_CANCELED,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(frame).await;
                                }
                                if let Ok(close_mark) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        }
                        let Some((_gen_left, tile_depleted)) = st_done
                            .consume_natural_generator(&region_id, tile, &spec, &gen_id)
                            .await
                        else {
                            if let Ok(packed) = (AbortMsg {
                                text: "Resource already depleted.".into(),
                            })
                            .pack()
                            {
                                if let Ok(frame) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_ABORT,
                                    &packed,
                                ) {
                                    let _ = tx_done.send(frame).await;
                                }
                            }
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(close_mark) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        };
                        let surv_snap = {
                            let mut live = st_done.live_clients.write().await;
                            live.get_mut(&eid).map(|c| {
                                c.apply_gather(done_now);
                                (
                                    c.to_survival_msg(done_now),
                                    c.life,
                                    c.current_stamina(done_now),
                                    c.energy,
                                    c.fatigue,
                                )
                            })
                        };
                        let surv_msg = surv_snap.map(|(m, life, stam, eng, fat)| {
                            let _ = st_done.db.update_survival(&eid, life, stam, eng, fat);
                            m
                        });
                        if let Some(sm) = surv_msg {
                            if let Ok(packed) = sm.pack() {
                                if let Ok(frame) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_SURVIVAL,
                                    &packed,
                                ) {
                                    let _ = tx_done.send(frame).await;
                                }
                            }
                        }
                        let new_rec = ItemRecord {
                            item_id: Uuid::new_v4().to_string(),
                            entity_id: eid.clone(),
                            prototype_id: proto_id,
                            name: item_name,
                            icon: item_icon,
                            level: gen_level,
                            size: item_size,
                            durability: 100.0,
                            max_durability: 100.0,
                            locked: false,
                            collectible_id: Some(collectible_id),
                            generator_id: Some(gen_id),
                            created_at: done_now,
                        };
                        let _ = st_done.db.insert_items(std::slice::from_ref(&new_rec));
                        let (actor_name, actor_ip) = st_done
                            .live_clients
                            .read()
                            .await
                            .get(&eid)
                            .map(|c| (c.name.clone(), c.peer_ip.clone()))
                            .unwrap_or_else(|| (eid.clone(), String::new()));
                        st_done.db.log_audit(
                            done_now,
                            "GATHER",
                            &actor_name,
                            &actor_ip,
                            &region_id,
                            &format!("Gathered {} (Lv.{})", new_rec.name, new_rec.level),
                        );
                        let item_msg =
                            item_record_to_msg(&new_rec, &st_done.catalog, done_now);

                        // Unity's InventorySystem adds items to live bag on InventoryUpdated (113)
                        let inv_upd = InventoryUpdatedMsg {
                            entity_id: eid.clone(),
                            items: vec![item_msg.clone()],
                            removed_item_ids: Vec::new(),
                            item_order: None,
                        };
                        if let Ok(packed) = inv_upd.pack() {
                            if let Ok(frame) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_INVENTORY_UPDATED,
                                &packed,
                            ) {
                                let _ = tx_done.send(frame).await;
                            }
                        }

                        let collected = CollectedMsg {
                            items: vec![item_msg],
                            result: RESULT_SUCCESS,
                            action_level: gen_level,
                            potential_level: gen_level,
                            success_ratio: 1.0,
                            ran_out: tile_depleted,
                        };
                        if let Ok(packed) = collected.pack() {
                            if let Ok(frame) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                reply_seq,
                                TC_COLLECTED,
                                &packed,
                            ) {
                                let _ = tx_done.send(frame).await;
                            }
                        }
                        if let Ok(empty) = pack_empty_struct() {
                            if let Ok(close_mark) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                reply_seq,
                                TC_REPLY_SEQUENCE_MARK,
                                &empty,
                            ) {
                                let _ = tx_done.send(close_mark).await;
                            }
                        }
                        if tile_depleted {
                            let disappear = DisappearEntityOnTileMsg {
                                entity_id: entity_id_arg,
                                tile_x: tile.0,
                                tile_y: tile.1,
                            };
                            if let Ok(packed) = disappear.pack() {
                                if let Ok(frame) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_DISAPPEAR_ENTITY_ON_TILE,
                                    &packed,
                                ) {
                                    st_done.broadcast_region_all(&region_id, &frame).await;
                                }
                            }
                        } else {
                            let changed = CollectibleChangedMsg {
                                entity_id: entity_id_arg,
                            };
                            if let Ok(packed) = changed.pack() {
                                if let Ok(frame) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_COLLECTIBLE_CHANGED,
                                    &packed,
                                ) {
                                    st_done.broadcast_region_all(&region_id, &frame).await;
                                }
                            }
                        }
                        award_xp(
                            &st_done,
                            &tx_done,
                            &seq_done,
                            &eid,
                            25,
                            Some((SKILL_CAT_GATHERING, 20)),
                        )
                        .await;
                    });
                }
                TC_GET_COLLECTIBLE => {
                    if !is_ready {
                        continue;
                    }
                    let req = GetCollectibleMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    // 1. Check slain animal carcass first
                    let mut reply_collectible: Option<CollectibleMsg> = None;
                    if !req.entity_id.is_empty() {
                        if let Some(animal) = state.animals.read().await.get(&req.entity_id).cloned() {
                            if animal.region_id == region_id && !animal.is_alive && !animal.butchered {
                                let gens = state
                                    .catalog
                                    .butcher_generators(animal.level)
                                    .into_iter()
                                    .filter_map(|g| {
                                        let rem = animal
                                            .butcher_stock
                                            .get(&g.id)
                                            .copied()
                                            .unwrap_or(g.amount);
                                        if rem <= 0 {
                                            return None;
                                        }
                                        Some(GeneratorMsg {
                                            id: g.id,
                                            level: g.level,
                                            name: g.name,
                                            icon: g.icon,
                                            amount: rem,
                                            effort: g.effort,
                                            duration: g.duration,
                                            tool_requirements: g.tool_requirements,
                                            enabled: true,
                                        })
                                    })
                                    .collect();
                                reply_collectible = Some(CollectibleMsg {
                                    entity_id: animal.entity_id,
                                    collectible_id: Some(animal.name),
                                    size: "medium".into(),
                                    generators: gens,
                                    critical_generator: String::new(),
                                });
                            }
                        }
                    }

                    // 2. Otherwise check natural resource at tile (tile_x, tile_y)
                    if reply_collectible.is_none() {
                        let terrain = state.terrains.get_or_load(region_id).await;
                        let removed = state.removed_naturals_snapshot(region_id).await;
                        let tile = (req.tile_x, req.tile_y);
                        if !removed.contains(&tile) {
                            let resolved_et = touched_naturals
                                .get(&tile)
                                .copied()
                                .or_else(|| terrain.natural_at(tile.0, tile.1));
                            if let Some(et) = resolved_et {
                                let spec = state.catalog.resolve_natural(et);
                                let stock = state
                                    .peek_natural_stock(region_id, tile, &spec)
                                    .await;
                                let gens = spec
                                    .generators
                                    .iter()
                                    .filter_map(|g| {
                                        let rem = stock.get(&g.id).copied().unwrap_or(g.amount);
                                        if rem <= 0 {
                                            return None;
                                        }
                                        Some(GeneratorMsg {
                                            id: g.id.clone(),
                                            level: g.level,
                                            name: g.name.clone(),
                                            icon: g.icon.clone(),
                                            amount: rem,
                                            effort: g.effort,
                                            duration: g.duration,
                                            tool_requirements: g.tool_requirements.clone(),
                                            enabled: true,
                                        })
                                    })
                                    .collect();
                                reply_collectible = Some(CollectibleMsg {
                                    entity_id: req.entity_id.clone(),
                                    collectible_id: Some(spec.collectible_id),
                                    size: "medium".into(),
                                    generators: gens,
                                    critical_generator: String::new(),
                                });
                            }
                        }
                    }

                    let col = reply_collectible.unwrap_or_else(|| CollectibleMsg {
                        entity_id: req.entity_id,
                        collectible_id: None,
                        size: String::new(),
                        generators: Vec::new(),
                        critical_generator: String::new(),
                    });
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_COLLECTIBLE,
                        &col.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_LOCK_OR_UNLOCK_ITEMS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = LockOrUnlockItemsMsg::unpack(&frame.msgpack)?;
                    let _ = state.db.set_items_locked(&eid, &req.item_ids, req.lock);
                    let now = ServerState::now_unix_secs();
                    state.db.log_audit(
                        now,
                        "ITEM",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!(
                            "{} {} item(s)",
                            if req.lock { "Locked" } else { "Unlocked" },
                            req.item_ids.len()
                        ),
                    );
                    let (_, infos) = build_inventory_msgs(&state, &eid, now);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY_INFOS,
                        &infos.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_DASHED => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    stop_resting_if_active(&state, &tx, &next_seq, &eid, now).await;
                    let surv_opt = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).and_then(|c| {
                            if !c.is_alive || c.current_stamina(now) + 0.01 < crate::state::DASH_STAMINA_COST {
                                return None;
                            }
                            c.consume_dash(now);
                            Some(c.to_survival_msg(now))
                        })
                    };
                    if let Some(surv) = surv_opt {
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &surv.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                    }
                }
                TC_RUN_UP => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = RunUpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let running = req.kind == 1 && req.on != 0;
                    let (normal_speed, battle_speed) = if running {
                        (RUN_NORMAL_SPEED, RUN_BATTLE_SPEED)
                    } else {
                        (WALK_NORMAL_SPEED, WALK_BATTLE_SPEED)
                    };
                    let run_down = RunDownMsg {
                        kind: 0,
                        json: format!(
                            "{{\"enabled\":true,\"running\":{running},\"mult\":{RUN_ANIM_MULT:.2},\"reason\":\"\"}}"
                        ),
                    };
                    let rd_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_RUN_DOWN,
                        &run_down.pack()?,
                    )?;
                    let _ = tx.send(rd_frame).await;

                    let spd = SetBaseMoveSpeedMsg {
                        entity_id: eid,
                        normal_speed,
                        battle_speed,
                    };
                    let spd_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SET_BASE_MOVE_SPEED,
                        &spd.pack()?,
                    )?;
                    let _ = tx.send(spd_frame).await;
                }
                TC_SAY_IN_EXCLUSIVE_CHANNEL => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let mut say = SayInExclusiveChannelMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();

                    // Check chat mute before broadcasting
                    if let Some(mute) = state
                        .db
                        .check_mute(Some(&authed_owner), Some(&eid), now)
                    {
                        let mut sys = say.clone();
                        sys.entity_id = "system".into();
                        sys.time = now;
                        sys.speaker_name = Some("[System]".into());
                        sys.speaker_freq = 0;
                        sys.text = if mute.reason.is_empty() {
                            "You are muted on this server.".into()
                        } else {
                            format!("You are muted: {}", mute.reason)
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SAY_IN_EXCLUSIVE_CHANNEL,
                            &sys.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    let trimmed_text = say.text.trim();
                    if trimmed_text.eq_ignore_ascii_case("/balloon") {
                        let region_id = authed_region
                            .as_deref()
                            .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                        let alive = {
                            let mut live = state.live_clients.write().await;
                            if let Some(client) = live.get_mut(&eid) {
                                if !client.is_alive {
                                    false
                                } else {
                                    client.stop_rest(now);
                                    client.boarding_on = BOARDING_ON_AIR_BALLOON;
                                    client.boarding_since = Some(now);
                                    client.boarding_until = Some(now + 900.0);
                                    client.vehicle_entity_id.clear();
                                    true
                                }
                            } else {
                                false
                            }
                        };
                        if alive {
                            broadcast_player_display_for(&state, &eid, region_id, now, &next_seq)
                                .await?;
                        }
                        continue;
                    }
                    // GM slash commands for configured DURANGO_ADMIN_KEYS
                    if trimmed_text.starts_with('/') && state.is_admin_owner(&authed_owner) {
                        let mut parts = trimmed_text.splitn(4, ' ');
                        let cmd = parts.next().unwrap_or("");
                        let arg1 = parts.next().unwrap_or("").trim();
                        let arg2 = parts.next().unwrap_or("").trim();
                        let arg3 = parts.next().unwrap_or("").trim();
                        let reply = match cmd {
                            "/who" => {
                                let clients = state.live_clients.read().await;
                                let names: Vec<String> = clients
                                    .values()
                                    .map(|c| format!("{} ({})", c.name, c.peer_ip))
                                    .collect();
                                format!("Online ({}): {}", names.len(), names.join(", "))
                            }
                            "/kick" if !arg1.is_empty() => {
                                if let Ok(Some(ch)) = state.db.resolve_character_by_name_or_id(arg1) {
                                    let kicked = state.kick_by_entity(&ch.entity_id).await;
                                    if kicked {
                                        format!("Kicked player {}.", ch.name)
                                    } else {
                                        format!("Player {} is not currently online.", ch.name)
                                    }
                                } else {
                                    format!("Unknown player: {arg1}")
                                }
                            }
                            "/ban" if !arg1.is_empty() => {
                                let hours: Option<f64> = arg2.parse::<f64>().ok().filter(|&h| h > 0.0);
                                let reason = if hours.is_some() { arg3 } else { arg2 };
                                let expires = hours.map(|h| now + h * 3600.0);
                                if let Ok(Some(ch)) = state.db.resolve_character_by_name_or_id(arg1) {
                                    let _ = state.db.add_ban("account", &ch.owner_key, reason, &authed_name, now, expires);
                                    let _ = state.db.add_ban("entity", &ch.entity_id, reason, &authed_name, now, expires);
                                    let kicked = state.kick_by_owner(&ch.owner_key).await;
                                    format!("Banned {} (account + entity, kicked {}).", ch.name, kicked)
                                } else {
                                    let _ = state.db.add_ban("name", arg1, reason, &authed_name, now, expires);
                                    format!("Banned name {arg1}.")
                                }
                            }
                            "/banip" if !arg1.is_empty() => {
                                let hours: Option<f64> = arg2.parse::<f64>().ok().filter(|&h| h > 0.0);
                                let reason = if hours.is_some() { arg3 } else { arg2 };
                                let expires = hours.map(|h| now + h * 3600.0);
                                let _ = state.db.add_ban("ip", arg1, reason, &authed_name, now, expires);
                                let kicked = state.kick_by_ip(arg1).await;
                                format!("Banned IP/CIDR {arg1} (kicked {kicked}).")
                            }
                            "/mute" if !arg1.is_empty() => {
                                let hours: Option<f64> = arg2.parse::<f64>().ok().filter(|&h| h > 0.0);
                                let reason = if hours.is_some() { arg3 } else { arg2 };
                                let expires = hours.map(|h| now + h * 3600.0);
                                if let Ok(Some(ch)) = state.db.resolve_character_by_name_or_id(arg1) {
                                    let _ = state.db.add_ban("mute", &ch.owner_key, reason, &authed_name, now, expires);
                                    format!("Muted player {}.", ch.name)
                                } else {
                                    format!("Unknown player: {arg1}")
                                }
                            }
                            "/unban" if !arg1.is_empty() => {
                                let mut removed = state.db.remove_bans_by_target(arg1).unwrap_or(0);
                                if let Ok(Some(ch)) = state.db.resolve_character_by_name_or_id(arg1) {
                                    removed += state.db.remove_bans_by_target(&ch.owner_key).unwrap_or(0);
                                    removed += state.db.remove_bans_by_target(&ch.entity_id).unwrap_or(0);
                                }
                                format!("Removed {removed} ban/mute entries for {arg1}.")
                            }
                            _ => "GM commands: /who | /kick <name> | /ban <name> [hours] [reason] | /banip <ip> [hours] [reason] | /mute <name> [hours] | /unban <target>".to_string(),
                        };
                    state.db.log_audit(
                            now,
                            "MODERATION",
                            &authed_name,
                            &peer_ip,
                            authed_region.as_deref().unwrap_or(""),
                            &format!("GM `{}` -> {}", trimmed_text, reply),
                        );
                        let mut sys = say.clone();
                        sys.entity_id = "system".into();
                        sys.time = now;
                        sys.speaker_name = Some("[Admin]".into());
                        sys.speaker_freq = 0;
                        sys.text = reply;
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SAY_IN_EXCLUSIVE_CHANNEL,
                            &sys.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    state.db.log_audit(
                        now,
                        "CHAT",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        trimmed_text,
                    );
                    say.entity_id = eid;
                    say.time = now;
                    say.speaker_name = Some(authed_name.clone());
                    say.speaker_freq = 0;
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SAY_IN_EXCLUSIVE_CHANNEL,
                        &say.pack()?,
                    )?;
                    state.broadcast_region_all(region_id, &bytes).await;
                }
                TC_EQUIP => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EquipMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let ok = if req.action == "equip" {
                        let valid_slot = is_valid_equip_slot(&req.slot_name);
                        let items = state.db.list_items(&eid).unwrap_or_default();
                        let item_opt = items.into_iter().find(|it| it.item_id == req.item_id);
                        let already_equipped_other = state
                            .db
                            .list_equipped(&eid)
                            .unwrap_or_default()
                            .into_iter()
                            .any(|(slot, iid)| iid == req.item_id && slot != req.slot_name);
                        if valid_slot
                            && !already_equipped_other
                            && item_opt.is_some_and(|it| {
                                !it.locked
                                    && state
                                        .catalog
                                        .is_item_equippable_in_slot(&it.prototype_id, &req.slot_name)
                            })
                        {
                            if req.slot_name.eq_ignore_ascii_case("both") {
                                let _ = state.db.clear_equipped(&eid, "main");
                                let _ = state.db.clear_equipped(&eid, "sub");
                            } else if req.slot_name.eq_ignore_ascii_case("main")
                                || req.slot_name.eq_ignore_ascii_case("sub")
                            {
                                let _ = state.db.clear_equipped(&eid, "both");
                            }
                            state.db.set_equipped(&eid, &req.slot_name, &req.item_id).is_ok()
                        } else {
                            false
                        }
                    } else if is_valid_equip_slot(&req.slot_name) {
                        state
                            .db
                            .clear_equipped(&eid, &req.slot_name)
                            .unwrap_or(false)
                    } else {
                        false
                    };
                    if !ok {
                        let abort = AbortMsg {
                            text: if req.action == "equip" {
                                "Cannot equip item.".into()
                            } else {
                                "Cannot unequip item.".into()
                            },
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let equip = build_equipments_msg(&state, &eid, now);
                    let eq_summary = if req.action == "equip" {
                        let it_name = equip
                            .slots
                            .get(&req.slot_name)
                            .map(|it| format!("{} (Lv.{})", it.name, it.level))
                            .unwrap_or_else(|| req.item_id.clone());
                        format!("Equipped {} in slot '{}'", it_name, req.slot_name)
                    } else {
                        format!("Unequipped slot '{}'", req.slot_name)
                    };
                    state.db.log_audit(
                        now,
                        "EQUIP",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &eq_summary,
                    );
                    let eq_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_EQUIPMENTS,
                        &equip.pack()?,
                    )?;
                    let _ = tx.send(eq_frame).await;

                    let mut appearance = state
                        .resolve_character(&eid)
                        .await
                        .map(|ch| state.build_character_appearance(&ch))
                        .unwrap_or_default();
                    if let Some(lc) = state.live_clients.read().await.get(&eid) {
                        apply_live_boarding(
                            &mut appearance,
                            lc.boarding_on,
                            lc.boarding_since,
                            lc.boarding_until,
                            &lc.vehicle_entity_id,
                        );
                    }
                    let display = PlayerDisplayMsg {
                        entity_id: eid.clone(),
                        equip: equip
                            .slots
                            .get("body")
                            .or_else(|| equip.slots.get("hoody"))
                            .map(|it| it.prototype.clone()),
                        head: equip.slots.get("head").map(|it| it.prototype.clone()),
                        appearance,
                    };
                    let disp_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_PLAYER_DISPLAY,
                        &display.pack()?,
                    )?;
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    state.broadcast_region_all(region_id, &disp_frame).await;

                    // Push updated combat action bar whenever gear/weapon changes
                    let actions = build_actions_msg(&state, &eid);
                    let act_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ACTIONS,
                        &actions.pack()?,
                    )?;
                    let _ = tx.send(act_frame).await;
                }
                TC_INVENTORY_ORDER => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = InventoryOrderMsg::unpack(&frame.msgpack)?;
                    if !req.is_artifact_target && !req.item_order.is_empty() {
                        let _ = state.db.update_item_order(&eid, &req.item_order);
                    }
                }
                TC_USE_ITEM => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = UseItemMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot use items while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    stop_resting_if_active(&state, &tx, &next_seq, &eid, now).await;
                    let found = state
                        .db
                        .list_items(&eid)
                        .unwrap_or_default()
                        .into_iter()
                        .find(|it| it.item_id == req.item_id);
                    let Some(item) = found else {
                        let abort = AbortMsg {
                            text: "Item not found in inventory.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    if item.locked {
                        let abort = AbortMsg {
                            text: "Cannot use a locked item.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if state.db.is_item_equipped(&eid, &item.item_id).unwrap_or(false) {
                        let abort = AbortMsg {
                            text: "Unequip item before using it.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if !state.catalog.is_consumable(&item.prototype_id) {
                        let abort = AbortMsg {
                            text: "This item cannot be consumed.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    let _ = state.db.delete_items(&eid, std::slice::from_ref(&item.item_id));
                    state.db.log_audit(
                        now,
                        "SURVIVAL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!("Consumed {} (Lv.{})", item.name, item.level),
                    );
                    let (life_delta, energy_delta, fatigue_delta) = state
                        .catalog
                        .consumable_effects(&item.prototype_id, item.level);
                    let surv_snap = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).map(|c| {
                            c.apply_eat_with(now, life_delta, energy_delta, fatigue_delta);
                            (
                                c.to_survival_msg(now),
                                c.life,
                                c.current_stamina(now),
                                c.energy,
                                c.fatigue,
                            )
                        })
                    };
                    let surv_opt = surv_snap.map(|(m, life, stam, eng, fat)| {
                        let _ = state.db.update_survival(&eid, life, stam, eng, fat);
                        m
                    });

                    let inv_upd = InventoryUpdatedMsg {
                        entity_id: eid.clone(),
                        items: Vec::new(),
                        removed_item_ids: vec![item.item_id],
                        item_order: None,
                    };
                    let upd_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY_UPDATED,
                        &inv_upd.pack()?,
                    )?;
                    let _ = tx.send(upd_frame).await;

                    if let Some(surv) = surv_opt {
                        let surv_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &surv.pack()?,
                        )?;
                        let _ = tx.send(surv_frame).await;
                    }

                    let ok_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok_frame).await;
                }
                TC_ESTIMATE_CRAFT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EstimateCraftMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let char_lv = state
                        .resolve_character(&eid)
                        .await
                        .map(|c| c.level.max(1))
                        .unwrap_or(1);
                    let estimation = if let Some(recipe) = state.catalog.resolve_recipe(&req.recipe_id) {
                        let owned_map: HashMap<String, ItemRecord> = state
                            .db
                            .list_items(&eid)
                            .unwrap_or_default()
                            .into_iter()
                            .map(|r| (r.item_id.clone(), r))
                            .collect();
                        let mut max_mat_lv = 1i32;
                        for ids in req.materials.values() {
                            for id in ids {
                                if let Some(rec) = owned_map.get(id) {
                                    max_mat_lv = max_mat_lv.max(rec.level);
                                }
                            }
                        }
                        let (prod_name, _, _) = state.catalog.prototype_info(&recipe.prototype_id);
                        let tags: BTreeMap<String, i32> = state
                            .catalog
                            .prototype_tags(&recipe.prototype_id)
                            .into_iter()
                            .collect();
                        Some(CraftEstimationMsg {
                            prototype_id: recipe.prototype_id.clone(),
                            level: max_mat_lv,
                            name: prod_name,
                            durability_min: 95.0,
                            durability_max: 105.0,
                            tags,
                            unrevealed_rare_tag_count: 0,
                            modifiable_count: 1,
                            success_rate: 1.0,
                            great_success_rate: 0.1,
                            required_ability_value: 0.0,
                        })
                    } else {
                        None
                    };
                    let resp = CraftEstimationInfoMsg {
                        craft_level: char_lv,
                        craft_estimation: estimation,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_CRAFT_ESTIMATION_INFO,
                        &resp.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_CRAFT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = CraftMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot craft while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    stop_resting_if_active(&state, &tx, &next_seq, &eid, now).await;

                    let Some(recipe) = state.catalog.resolve_recipe(&req.recipe_id).cloned() else {
                        let abort = AbortMsg {
                            text: format!("Unknown recipe: {}", req.recipe_id),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };

                    let owned_map: HashMap<String, ItemRecord> = state
                        .db
                        .list_items(&eid)
                        .unwrap_or_default()
                        .into_iter()
                        .map(|r| (r.item_id.clone(), r))
                        .collect();
                    let equipped_ids = state.db.equipped_item_ids(&eid).unwrap_or_default();

                    let mut consumed_ids = Vec::new();
                    let mut seen_ids = HashSet::new();
                    let mut mat_err: Option<String> = None;
                    let mut max_mat_level = 1i32;

                    for slot in &recipe.slots {
                        let ids = req
                            .materials
                            .get(&slot.slot_id)
                            .map(Vec::as_slice)
                            .unwrap_or(&[]);
                        if ids.is_empty() && slot.count_min == 0 {
                            continue;
                        }
                        if ids.len() < slot.count_min || ids.len() > slot.count_max {
                            mat_err = Some(format!(
                                "Invalid material count for slot {}",
                                slot.slot_id
                            ));
                            break;
                        }
                        for id in ids {
                            if !seen_ids.insert(id.clone()) {
                                mat_err = Some("Duplicate material item.".into());
                                break;
                            }
                            let Some(rec) = owned_map.get(id) else {
                                mat_err = Some("Material item not found in inventory.".into());
                                break;
                            };
                            if rec.locked {
                                mat_err = Some("Cannot craft with a locked item.".into());
                                break;
                            }
                            if equipped_ids.contains(id) {
                                mat_err = Some("Unequip item before crafting with it.".into());
                                break;
                            }
                            if !slot.required_tags.is_empty() || !slot.required_materials.is_empty() {
                                let item_tags: HashSet<String> = state
                                    .catalog
                                    .prototype_tags(&rec.prototype_id)
                                    .into_iter()
                                    .map(|(t, _)| t)
                                    .collect();
                                let tags_ok = slot.required_tags.is_empty()
                                    || slot.required_tags.iter().any(|t| item_tags.contains(t));
                                let mats_ok = slot.required_materials.is_empty()
                                    || slot
                                        .required_materials
                                        .iter()
                                        .any(|t| item_tags.contains(t));
                                if !tags_ok || !mats_ok {
                                    mat_err = Some(format!(
                                        "{} does not match requirements for slot {}.",
                                        rec.name, slot.slot_id
                                    ));
                                    break;
                                }
                            }
                            max_mat_level = max_mat_level.max(rec.level);
                            consumed_ids.push(id.clone());
                        }
                        if mat_err.is_some() {
                            break;
                        }
                    }

                    if let Some(reason) = mat_err {
                        let abort = AbortMsg { text: reason };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if owned_map.len().saturating_sub(consumed_ids.len()) + 1 > MAX_INVENTORY_ITEMS
                    {
                        let abort = AbortMsg {
                            text: "Inventory is full.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    if let Some(prev_cancel) = active_craft_cancel.take() {
                        let _ = prev_cancel.send(());
                    }

                    // Reply sequence on h.seq: ReplySequenceMark -> Timer -> (delay / cancel) -> InventoryUpdated + Survival + Crafted -> ReplySequenceMark
                    let open_mark = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REPLY_SEQUENCE_MARK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(open_mark).await;

                    let delay_ms = state.config.craft_delay_ms;
                    let timer_dur = (delay_ms as f32 / 1000.0).max(0.05);
                    let timer_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_TIMER,
                        &TimerMsg {
                            duration: timer_dur,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(timer_frame).await;

                    let (cancel_tx, cancel_rx) = oneshot::channel::<()>();
                    active_craft_cancel = Some(cancel_tx);
                    let my_craft_gen = active_craft_gen.fetch_add(1, Ordering::Relaxed) + 1;
                    let craft_gen_ref = active_craft_gen.clone();
                    let st_done = state.clone();
                    let tx_done = tx.clone();
                    let seq_done = next_seq.clone();
                    let reply_seq = h.seq;
                    tokio::spawn(async move {
                        let cancelled = if delay_ms > 0 {
                            tokio::select! {
                                _ = tokio::time::sleep(Duration::from_millis(delay_ms)) => false,
                                _ = cancel_rx => true,
                            }
                        } else {
                            false
                        };
                        let done_now = ServerState::now_unix_secs();
                        if cancelled || craft_gen_ref.load(Ordering::Relaxed) != my_craft_gen {
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(cf) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_CANCELED,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(cf).await;
                                }
                                if let Ok(close_mark) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        }
                        if !is_entity_alive(&st_done, &eid).await {
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(cf) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_CANCELED,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(cf).await;
                                }
                                if let Ok(close_mark) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        }
                        // Re-verify materials are still owned, unlocked, and unequipped right before consuming
                        let cur_items: HashMap<String, ItemRecord> = st_done
                            .db
                            .list_items(&eid)
                            .unwrap_or_default()
                            .into_iter()
                            .map(|r| (r.item_id.clone(), r))
                            .collect();
                        let cur_eq = st_done.db.equipped_item_ids(&eid).unwrap_or_default();
                        let mats_still_ok = consumed_ids.iter().all(|id| {
                            cur_items
                                .get(id)
                                .is_some_and(|r| !r.locked && !cur_eq.contains(id))
                        });
                        if !mats_still_ok
                            || st_done.db.delete_items(&eid, &consumed_ids).is_err()
                        {
                            if let Ok(packed) = (AbortMsg {
                                text: "Crafting materials are no longer available.".into(),
                            })
                            .pack()
                            {
                                if let Ok(af) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_ABORT,
                                    &packed,
                                ) {
                                    let _ = tx_done.send(af).await;
                                }
                            }
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(close_mark) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        }

                        let (prod_name, prod_icon, prod_size) =
                            st_done.catalog.prototype_info(&recipe.prototype_id);
                        let product_rec = ItemRecord {
                            item_id: Uuid::new_v4().to_string(),
                            entity_id: eid.clone(),
                            prototype_id: recipe.prototype_id.clone(),
                            name: prod_name,
                            icon: prod_icon,
                            level: max_mat_level,
                            size: prod_size,
                            durability: 100.0,
                            max_durability: 100.0,
                            locked: false,
                            collectible_id: None,
                            generator_id: None,
                            created_at: done_now,
                        };
                        let _ = st_done
                            .db
                            .insert_items(std::slice::from_ref(&product_rec));
                        let product_msg = item_record_to_msg(&product_rec, &st_done.catalog, done_now);

                        let inv_upd = InventoryUpdatedMsg {
                            entity_id: eid.clone(),
                            items: vec![product_msg.clone()],
                            removed_item_ids: consumed_ids,
                            item_order: None,
                        };
                        if let Ok(packed) = inv_upd.pack() {
                            if let Ok(upd_frame) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_INVENTORY_UPDATED,
                                &packed,
                            ) {
                                let _ = tx_done.send(upd_frame).await;
                            }
                        }

                        let surv_snap = {
                            let mut live = st_done.live_clients.write().await;
                            live.get_mut(&eid).map(|c| {
                                c.apply_craft(done_now, recipe.energy);
                                (
                                    c.to_survival_msg(done_now),
                                    c.life,
                                    c.current_stamina(done_now),
                                    c.energy,
                                    c.fatigue,
                                )
                            })
                        };
                        let surv_opt = surv_snap.map(|(m, life, stam, eng, fat)| {
                            let _ = st_done.db.update_survival(&eid, life, stam, eng, fat);
                            m
                        });
                        if let Some(surv) = surv_opt {
                            if let Ok(packed) = surv.pack() {
                                if let Ok(surv_frame) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_SURVIVAL,
                                    &packed,
                                ) {
                                    let _ = tx_done.send(surv_frame).await;
                                }
                            }
                        }

                        let (actor_name, actor_ip, actor_reg) = st_done
                            .live_clients
                            .read()
                            .await
                            .get(&eid)
                            .map(|c| (c.name.clone(), c.peer_ip.clone(), c.region_id.clone()))
                            .unwrap_or_else(|| (eid.clone(), String::new(), String::new()));
                        st_done.db.log_audit(
                            done_now,
                            "CRAFT",
                            &actor_name,
                            &actor_ip,
                            &actor_reg,
                            &format!("Crafted {} (Lv.{})", product_msg.name, product_msg.level),
                        );
                        let crafted = CraftedMsg {
                            result: RESULT_SUCCESS,
                            action_level: max_mat_level,
                            potential_level: max_mat_level,
                            success_ratio: 1.0,
                            items: vec![product_msg],
                        };
                        if let Ok(packed) = crafted.pack() {
                            if let Ok(frame) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                reply_seq,
                                TC_CRAFTED,
                                &packed,
                            ) {
                                let _ = tx_done.send(frame).await;
                            }
                        }
                        if let Ok(empty) = pack_empty_struct() {
                            if let Ok(close_mark) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                reply_seq,
                                TC_REPLY_SEQUENCE_MARK,
                                &empty,
                            ) {
                                let _ = tx_done.send(close_mark).await;
                            }
                        }
                        award_xp(
                            &st_done,
                            &tx_done,
                            &seq_done,
                            &eid,
                            35,
                            Some((recipe.skill_category, 25)),
                        )
                        .await;
                    });
                }
                TC_OCCUPY_ARTIFACT_SITE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = OccupyArtifactSiteMsg::unpack(&frame.msgpack)?;
                    let region_id = authed_region
                        .clone()
                        .unwrap_or_else(|| crate::state::DEFAULT_REGION_CODE.to_string());
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let now = ServerState::now_unix_secs();

                    let bp_id = req
                        .blueprint_id
                        .clone()
                        .filter(|s| !s.is_empty())
                        .unwrap_or_else(|| "tent".to_string());
                    let spec = state.catalog.resolve_blueprint(&bp_id);
                    // Derive authoritative footprint from catalog spec (respecting 90/270 deg rotation)
                    let (size_x, size_y) = if req.rotation.rem_euclid(2) == 1 {
                        (spec.size_y.max(1), spec.size_x.max(1))
                    } else {
                        (spec.size_x.max(1), spec.size_y.max(1))
                    };

                    if let Some((px, py, is_alive)) = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .map(|c| (c.pos_x, c.pos_y, c.is_alive))
                    {
                        if !is_alive {
                            let abort = AbortMsg {
                                text: "Cannot build while unconscious.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                        let cx = f32::from(req.tile_x) + f32::from(size_x) * 0.5;
                        let cy = f32::from(req.tile_y) + f32::from(size_y) * 0.5;
                        if ((px / 200.0) - cx).hypot((py / 200.0) - cy) > 16.0 {
                            let abort = AbortMsg {
                                text: "Build site is too far away.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    }

                    if usize::from(req.tile_x) + usize::from(size_x) > terrain.width
                        || usize::from(req.tile_y) + usize::from(size_y) > terrain.height
                    {
                        let abort = AbortMsg {
                            text: "Placement out of island bounds.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    if let Ok(Some(estate)) = state.db.find_active_estate_for_tile(
                        &region_id,
                        req.tile_x,
                        req.tile_y,
                        size_x,
                        size_y,
                        now,
                    ) {
                        if !is_estate_owner(&state, &eid, &estate)
                            && !state.is_admin_owner(&authed_owner)
                            && estate.access_for_others < 2
                        {
                            let abort = AbortMsg {
                                text: "Cannot build inside another survivor's private domain.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    }

                    let mut all_dry = true;
                    'dry: for dx in 0..size_x {
                        for dy in 0..size_y {
                            if !terrain.is_dry_land(req.tile_x + dx, req.tile_y + dy) {
                                all_dry = false;
                                break 'dry;
                            }
                        }
                    }
                    if !all_dry {
                        let abort = AbortMsg {
                            text: "Cannot build in water.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    let existing = state
                        .db
                        .list_artifacts_in_region(&region_id)
                        .unwrap_or_default();
                    let overlaps = existing.iter().any(|a| {
                        rects_overlap(
                            req.tile_x,
                            req.tile_y,
                            size_x,
                            size_y,
                            a.tile_x,
                            a.tile_y,
                            a.size_x,
                            a.size_y,
                        )
                    });
                    if overlaps {
                        let abort = AbortMsg {
                            text: "Tile is already occupied by another structure.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    let art_id = Uuid::new_v4().to_string();
                    let builder_lv = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .map_or(1u8, |c| c.level.clamp(1, 60) as u8);
                    let rec = ArtifactRecord {
                        entity_id: art_id.clone(),
                        region_id: region_id.clone(),
                        entity_type: spec.entity_type,
                        blueprint_id: bp_id,
                        tile_x: req.tile_x,
                        tile_y: req.tile_y,
                        size_x,
                        size_y,
                        height: spec.height,
                        rotation: req.rotation,
                        building_state: BUILDING_STATE_OCCUPIED,
                        founder_entity_id: eid,
                        level: builder_lv,
                        created_at: now,
                        durability: 100.0,
                        max_durability: 100.0,
                        last_decay_at: now,
                        gate_opened: false,
                        is_lit: false,
                        materials_json: "{}".to_string(),
                    };
                    let _ = state.db.upsert_artifact(&rec);
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!(
                            "Placed construction site '{}' at ({}, {})",
                            rec.blueprint_id, rec.tile_x, rec.tile_y
                        ),
                    );

                    let appear = artifact_record_to_appear_msg(&rec, &state.catalog, now);
                    let appear_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_APPEAR_ARTIFACT,
                        &appear.pack()?,
                    )?;
                    state.broadcast_region_all(&region_id, &appear_frame).await;

                    // Reply sequence on h.seq: ReplySequenceMark -> Timer -> Occupied -> ReplySequenceMark
                    let open_mark = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REPLY_SEQUENCE_MARK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(open_mark).await;

                    let delay_ms = state.config.build_delay_ms;
                    let timer_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_TIMER,
                        &TimerMsg {
                            duration: (delay_ms as f32 / 1000.0).max(0.05),
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(timer_frame).await;

                    let occ = OccupiedMsg {
                        entity_id: art_id,
                        tile_x: i32::from(req.tile_x),
                        tile_y: i32::from(req.tile_y),
                        floor: req.floor,
                    };
                    let occ_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OCCUPIED,
                        &occ.pack()?,
                    )?;
                    let _ = tx.send(occ_frame).await;

                    let close_mark = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REPLY_SEQUENCE_MARK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(close_mark).await;
                }
                TC_BUILD_ARTIFACT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = BuildArtifactMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot build while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let cur_region = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let Some(art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let abort = AbortMsg {
                            text: "Structure not found.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    if art.region_id != cur_region || art.building_state != BUILDING_STATE_OCCUPIED {
                        let abort = AbortMsg {
                            text: "Structure is not ready to be built.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    let open_mark = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REPLY_SEQUENCE_MARK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(open_mark).await;

                    let delay_ms = state.config.build_delay_ms;
                    let timer_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_TIMER,
                        &TimerMsg {
                            duration: (delay_ms as f32 / 1000.0).max(0.05),
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(timer_frame).await;

                    let st_done = state.clone();
                    let tx_done = tx.clone();
                    let seq_done = next_seq.clone();
                    let reply_seq = h.seq;
                    let art_id = art.entity_id;
                    let art_bp = art.blueprint_id;
                    let art_tx = art.tile_x;
                    let art_ty = art.tile_y;
                    let region_id = art.region_id;
                    let actor_name = authed_name.clone();
                    let actor_ip = peer_ip.clone();
                    tokio::spawn(async move {
                        if delay_ms > 0 {
                            tokio::time::sleep(Duration::from_millis(delay_ms)).await;
                        }
                        let transitioned = st_done
                            .db
                            .update_artifact_state_if(
                                &art_id,
                                BUILDING_STATE_OCCUPIED,
                                BUILDING_STATE_BUILT,
                            )
                            .unwrap_or(false);
                        let done_now = ServerState::now_unix_secs();
                        if !transitioned {
                            if let Ok(packed) = (AbortMsg {
                                text: "Structure is no longer under construction.".into(),
                            })
                            .pack()
                            {
                                if let Ok(af) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_ABORT,
                                    &packed,
                                ) {
                                    let _ = tx_done.send(af).await;
                                }
                            }
                            if let Ok(empty) = pack_empty_struct() {
                                if let Ok(close_mark) = encode_frame(
                                    done_now,
                                    seq_done.fetch_add(1, Ordering::Relaxed),
                                    reply_seq,
                                    TC_REPLY_SEQUENCE_MARK,
                                    &empty,
                                ) {
                                    let _ = tx_done.send(close_mark).await;
                                }
                            }
                            return;
                        }
                        st_done.db.log_audit(
                            done_now,
                            "BUILD",
                            &actor_name,
                            &actor_ip,
                            &region_id,
                            &format!(
                                "Constructed frame for '{}' at ({}, {})",
                                art_bp, art_tx, art_ty
                            ),
                        );
                        let built = ArtifactBuiltMsg {
                            entity_id: art_id,
                            builder_id: eid,
                        };
                        if let Ok(packed) = built.pack() {
                            if let Ok(frame) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                reply_seq,
                                TC_ARTIFACT_BUILT,
                                &packed,
                            ) {
                                let _ = tx_done.send(frame).await;
                            }
                            if let Ok(bcast) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ARTIFACT_BUILT,
                                &packed,
                            ) {
                                st_done
                                    .broadcast_region_except(&region_id, &built.builder_id, &bcast)
                                    .await;
                            }
                        }
                        if let Ok(empty) = pack_empty_struct() {
                            if let Ok(close_mark) = encode_frame(
                                done_now,
                                seq_done.fetch_add(1, Ordering::Relaxed),
                                reply_seq,
                                TC_REPLY_SEQUENCE_MARK,
                                &empty,
                            ) {
                                let _ = tx_done.send(close_mark).await;
                            }
                        }
                    });
                }
                TC_COMPLETE_ARTIFACT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = CompleteArtifactMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot complete construction while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let cur_region = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let Some(art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let abort = AbortMsg {
                            text: "Structure not found.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    if art.region_id != cur_region || art.building_state != BUILDING_STATE_BUILT {
                        let abort = AbortMsg {
                            text: "Structure cannot be completed in its current state.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let mut completed_art = art.clone();
                    completed_art.building_state = BUILDING_STATE_COMPLETED;
                    let _ = state
                        .db
                        .update_artifact_state(&art.entity_id, BUILDING_STATE_COMPLETED);

                    let appear = artifact_record_to_appear_msg(&completed_art, &state.catalog, now);
                    let disp_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        durango_wire::TC_ARTIFACT_DISPLAY,
                        &appear.display_msg().pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &disp_frame).await;
                    let state_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        durango_wire::TC_ARTIFACT_STATE,
                        &appear.state_msg().pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &state_frame).await;

                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        &art.region_id,
                        &format!(
                            "Completed structure '{}' at ({}, {})",
                            art.blueprint_id, art.tile_x, art.tile_y
                        ),
                    );
                    let comp = ArtifactCompletedMsg {
                        entity_id: art.entity_id,
                    };
                    let bcast = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ARTIFACT_COMPLETED,
                        &comp.pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &bcast).await;

                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    award_xp(
                        &state,
                        &tx,
                        &next_seq,
                        &eid,
                        40,
                        Some((SKILL_CAT_CONSTRUCTING, 30)),
                    )
                    .await;
                }
                TC_REST_ON => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let _req = RestOnMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let rest_snap = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).map(|c| {
                            if !c.is_alive {
                                return Err("Cannot rest while unconscious.");
                            }
                            if c.in_battle {
                                return Err("Cannot rest during combat.");
                            }
                            let needs_fatigue = c.fatigue > 0.001;
                            c.apply_rest(now);
                            Ok((
                                c.to_survival_msg(now),
                                build_status_effects_msg(c, now),
                                (c.life, c.current_stamina(now), c.energy, c.fatigue),
                                needs_fatigue,
                            ))
                        })
                    };
                    let Some(rest_res) = rest_snap else {
                        continue;
                    };
                    let (surv, st_msg, (life, stam, eng, fat), needs_fatigue) = match rest_res {
                        Ok(v) => v,
                        Err(reason) => {
                            let abort = AbortMsg { text: reason.into() };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    };
                    let _ = state.db.update_survival(&eid, life, stam, eng, fat);
                    state.db.log_audit(
                        now,
                        "SURVIVAL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        "Started resting (-2.0 Fatigue/s)",
                    );

                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let surv_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SURVIVAL,
                        &surv.pack()?,
                    )?;
                    let _ = tx.send(surv_frame).await;
                    send_fatigue_velocity(
                        &tx,
                        &next_seq,
                        now,
                        if needs_fatigue { -REST_FATIGUE_PER_SEC } else { 0.0 },
                    )
                    .await;
                    let st_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_STATUS_EFFECTS,
                        &st_msg.pack()?,
                    )?;
                    let _ = tx.send(st_frame).await;
                }
                TC_DESTRUCT_ARTIFACT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = DestructArtifactMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let Some(art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let abort = AbortMsg {
                            text: "Structure not found.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };

                    if art.entity_id.starts_with("poi-") {
                        let abort = AbortMsg {
                            text: "World landmarks cannot be dismantled.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, true)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }

                    let _ = state.db.delete_artifact(&art.entity_id);
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        &art.region_id,
                        &format!(
                            "Dismantled structure '{}' at ({}, {})",
                            art.blueprint_id, art.tile_x, art.tile_y
                        ),
                    );
                    let destr = DestructingMsg {
                        duration: 0.1,
                        tool_type: 0,
                    };
                    let reply = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_DESTRUCTING,
                        &destr.pack()?,
                    )?;
                    let _ = tx.send(reply).await;

                    let dis = DisappearEntity {
                        entity_id: art.entity_id,
                    };
                    let bcast = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_DISAPPEAR_ENTITY,
                        &dis.pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &bcast).await;
                }
                TC_GET_ACTIONS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let actions = build_actions_msg(&state, &eid);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ACTIONS,
                        &actions.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_SELECT_BATTLE_TARGET => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SelectBattleTargetMsg::unpack(&frame.msgpack)?;
                    let target = if req.entity_id.is_empty() {
                        None
                    } else {
                        Some(req.entity_id)
                    };
                    if let Some(c) = state.live_clients.write().await.get_mut(&eid) {
                        c.battle_target_id = target;
                    }
                    let now = ServerState::now_unix_secs();
                    let actions = build_actions_msg(&state, &eid);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ACTIONS,
                        &actions.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_ENTER_BATTLE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EnterBattleMsg::unpack(&frame.msgpack)?;
                    if let Some(c) = state.live_clients.write().await.get_mut(&eid) {
                        c.battle_target_id = Some(req.entity_id.clone());
                        c.in_battle = true;
                    }
                    let now = ServerState::now_unix_secs();
                    let begun = BattleBegunMsg {
                        entity_id: eid,
                        event_at: now,
                        enemy_id: req.entity_id,
                        start_damaged: false,
                    };
                    let bcast = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_BATTLE_BEGUN,
                        &begun.pack()?,
                    )?;
                    let _ = tx.send(bcast).await;
                }
                TC_EXIT_BATTLE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let now = ServerState::now_unix_secs();
                    let player_pos = {
                        let live = state.live_clients.read().await;
                        live.get(&eid).map(|c| (c.pos_x, c.pos_y))
                    };
                    let Some((px, py)) = player_pos else {
                        continue;
                    };
                    let mut released_ids: Vec<String> = Vec::new();
                    let mut still_in_battle = false;
                    {
                        let mut animals = state.animals.write().await;
                        for a in animals.values_mut() {
                            if a.enemy_id.as_deref() == Some(eid.as_str()) {
                                let (ax, ay) = a.current_pos(now);
                                if (px - ax).hypot(py - ay) > 1200.0 || !a.is_aggressive {
                                    a.enemy_id = None;
                                    released_ids.push(a.entity_id.clone());
                                } else if a.is_alive && a.region_id == region_id {
                                    still_in_battle = true;
                                }
                            }
                        }
                    }
                    for aid in released_ids {
                        let peace_ci = build_combat_interaction_msg(
                            &aid,
                            &eid,
                            ANIMAL_STATUS_PEACE,
                            false,
                            None,
                        );
                        if let Ok(packed) = peace_ci.pack() {
                            if let Ok(ci_frame) = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_COMBAT_INTERACTION,
                                &packed,
                            ) {
                                state.broadcast_region_all(region_id, &ci_frame).await;
                            }
                        }
                    }
                    if !still_in_battle {
                        if let Some(c) = state.live_clients.write().await.get_mut(&eid) {
                            c.in_battle = false;
                            c.battle_target_id = None;
                        }
                        let ended = BattleEndedMsg {
                            entity_id: eid,
                            time: now,
                        };
                        let bcast = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_BATTLE_ENDED,
                            &ended.pack()?,
                        )?;
                        let _ = tx.send(bcast).await;
                    }
                }
                TC_USE_BATTLE_ACTION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = UseBattleActionMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    stop_resting_if_active(&state, &tx, &next_seq, &eid, now).await;
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);

                    let Some(action) = state.catalog.resolve_battle_action(&req.action_id).cloned() else {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    };

                    // Snapshot alive, stamina, position, battle state & cooldown atomically
                    let client_snap = {
                        let live = state.live_clients.read().await;
                        live.get(&eid).map(|c| {
                            (
                                c.is_alive,
                                c.current_stamina(now),
                                c.pos_x,
                                c.pos_y,
                                c.in_battle,
                                c.battle_target_id.clone(),
                                c.last_battle_action_at,
                            )
                        })
                    };
                    let Some((player_alive, cur_stamina, px, py, was_in_battle, saved_target, last_action_at)) =
                        client_snap
                    else {
                        continue;
                    };
                    if !player_alive {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    }
                    let cooldown_secs = (action.cooltime as f64 - 0.15).max(0.15);
                    if last_action_at > 0.0 && now - last_action_at < cooldown_secs {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    }
                    if action.stamina > 0 && cur_stamina + 0.01 < action.stamina as f32 {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    }

                    if action.is_dodge {
                    state.db.log_audit(
                            now,
                            "COMBAT",
                            &authed_name,
                            &peer_ip,
                            region_id,
                            &format!("Executed dodge ({})", action.id),
                        );
                        let surv_opt = {
                            let mut live = state.live_clients.write().await;
                            live.get_mut(&eid).map(|c| {
                                if action.stamina > 0 {
                                    c.consume_stamina(now, action.stamina as f32);
                                }
                                c.last_dodge_at = now;
                                c.last_battle_action_at = now;
                                c.to_survival_msg(now)
                            })
                        };
                        if let Some(surv) = surv_opt {
                            let surv_frame = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_SURVIVAL,
                                &surv.pack()?,
                            )?;
                            let _ = tx.send(surv_frame).await;
                        }
                        if h.seq != 0 {
                            let ok = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_OK,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(ok).await;
                        }
                        continue;
                    }

                    let equipped_slots = state.db.list_equipped(&eid).unwrap_or_default();
                    let weapon_item_id = equipped_slots
                        .iter()
                        .find(|(slot, _)| is_weapon_slot(slot))
                        .map(|(_, item_id)| item_id.clone());
                    let mut equipped_weapon = weapon_item_id
                        .as_deref()
                        .and_then(|wid| state.db.get_item(&eid, wid).ok().flatten());
                    let has_weapon = equipped_weapon.is_some();
                    let is_ranged_action = action.id.starts_with("ranged_");
                    let weapon_ok = if action.id.starts_with("barehand_") {
                        true
                    } else if is_ranged_action {
                        equipped_weapon.as_ref().is_some_and(|it| {
                            let proto_lower = it.prototype_id.to_ascii_lowercase();
                            proto_lower.contains("bow")
                                || proto_lower.contains("sling")
                                || proto_lower.contains("ranged")
                                || state
                                    .catalog
                                    .prototype_tags(&it.prototype_id)
                                    .into_iter()
                                    .any(|(t, _)| {
                                        state
                                            .catalog
                                            .battle_actions_for_tags(&[t])
                                            .iter()
                                            .any(|a| a.id.starts_with("ranged_"))
                                    })
                        })
                    } else {
                        // onehand_* / twohand_* / skill actions require an equipped weapon
                        has_weapon
                    };
                    if !weapon_ok {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    }

                    let target_id = req
                        .target_entity_id
                        .filter(|s| !s.is_empty())
                        .or(saved_target);
                    let Some(target_id) = target_id else {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    };

                    // Verify target is alive, in this region, and within weapon reach before spending stamina
                    let is_barehand = action.id.starts_with("barehand_");
                    let weapon_bonus = if is_barehand {
                        0
                    } else {
                        equipped_weapon
                            .as_ref()
                            .map_or(0, |w| state.catalog.weapon_attack_bonus(&w.prototype_id, w.level))
                    };
                    let dmg_val = action.damage + weapon_bonus;
                    let attack_type = if !is_barehand && has_weapon {
                        ATTACK_TYPE_DAGGER
                    } else {
                        ATTACK_TYPE_BARE_HANDS
                    };
                    let max_range = action.use_range.max(320.0);

                    let hit_outcome = {
                        let mut animals = state.animals.write().await;
                        if let Some(a) = animals.get_mut(&target_id) {
                            let (ax, ay) = a.current_pos(now);
                            let dist = (px - ax).hypot(py - ay);
                            if a.region_id == region_id
                                && a.is_alive
                                && !a.butchered
                                && dist <= max_range
                            {
                                a.hp = (a.hp - dmg_val as f32).max(0.0);
                                a.pos_x = ax;
                                a.pos_y = ay;
                                a.walk_from_x = ax;
                                a.walk_from_y = ay;
                                a.walk_start_at = 0.0;
                                a.walk_end_at = 0.0;
                                a.stop_walking_at = 0.0;
                                a.stand_at = 0.0;
                                a.enemy_id = Some(eid.clone());
                                a.aggro_seen_at = now;
                                a.threat_telegraph_until = 0.0;
                                let died = a.hp <= 0.0;
                                if died {
                                    a.is_alive = false;
                                    a.enemy_id = None;
                                    a.butcher_stock.clear();
                                    for g in state.catalog.butcher_generators(a.level) {
                                        a.butcher_stock.insert(g.id, g.amount.max(1));
                                    }
                                    a.respawn_at = now + state.config.wildlife_respawn_secs;
                                }
                                Some((a.hp, a.max_hp, died, ax, ay, a.yaw, a.dead_motion.clone()))
                            } else {
                                None
                            }
                        } else {
                            None
                        }
                    };

                    let Some((new_hp, max_hp, died, ax, ay, ayaw, dead_motion)) = hit_outcome else {
                        if h.seq != 0 {
                            let fail = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_FAILED,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(fail).await;
                        }
                        continue;
                    };
                    state.db.log_audit(
                        now,
                        "COMBAT",
                        &authed_name,
                        &peer_ip,
                        region_id,
                        &format!(
                            "Used '{}' on {} (-{} HP, {:.0}/{:.0} HP left)",
                            action.id, target_id, dmg_val, new_hp, max_hp
                        ),
                    );

                    // Apply weapon tool wear (-1.5 durability per landed combat strike)
                    if let Some(ref mut item) = equipped_weapon {
                        let next_dur = (item.durability - 1.5).max(1.0);
                        if (next_dur - item.durability).abs() > 0.01 {
                            item.durability = next_dur;
                            let _ = state
                                .db
                                .update_item_durability(&eid, &item.item_id, next_dur);
                            let upd = InventoryUpdatedMsg {
                                entity_id: eid.clone(),
                                items: vec![item_record_to_msg(item, &state.catalog, now)],
                                removed_item_ids: Vec::new(),
                                item_order: None,
                            };
                            if let Ok(packed) = upd.pack() {
                                if let Ok(uf) = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_INVENTORY_UPDATED,
                                    &packed,
                                ) {
                                    let _ = tx.send(uf).await;
                                }
                            }
                        }
                    }

                    let surv_opt = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).map(|c| {
                            if action.stamina > 0 {
                                c.consume_stamina(now, action.stamina as f32);
                            }
                            if !was_in_battle {
                                c.in_battle = true;
                                c.battle_target_id = Some(target_id.clone());
                            }
                            c.last_battle_action_at = now;
                            c.to_survival_msg(now)
                        })
                    };
                    if let Some(surv) = surv_opt {
                        let surv_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &surv.pack()?,
                        )?;
                        let _ = tx.send(surv_frame).await;
                    }

                    if !was_in_battle {
                        let begun = BattleBegunMsg {
                            entity_id: eid.clone(),
                            event_at: now,
                            enemy_id: target_id.clone(),
                            start_damaged: false,
                        };
                        let bcast = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_BATTLE_BEGUN,
                            &begun.pack()?,
                        )?;
                        let _ = tx.send(bcast).await;
                    }

                    {
                        let damaged = DamagedMsg {
                            victim_id: target_id.clone(),
                            attacker_id: eid.clone(),
                            damage: DamageMsg {
                                result: DAMAGE_RESULT_HIT,
                                value: dmg_val,
                                part: BODY_PART_BODY,
                                direction: DAMAGE_DIR_FRONT,
                                attack_type,
                                effects: 0,
                            },
                            event_at: now,
                        };
                        let dmg_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_DAMAGED,
                            &damaged.pack()?,
                        )?;
                        state.broadcast_region_all(region_id, &dmg_frame).await;

                        let mut agauges = BTreeMap::new();
                        agauges.insert("groggy".into(), GaugeMsg::constant(now, 0.0, 100.0));
                        let asurv = SurvivalMsg {
                            entity_id: target_id.clone(),
                            life: GaugeMsg::constant(now, new_hp, max_hp),
                            gauges: agauges,
                        };
                        let asurv_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &asurv.pack()?,
                        )?;
                        state.broadcast_region_all(region_id, &asurv_frame).await;

                        if died {
                            // Broadcast dead collapse animation (Move with dead_motion, play once) +
                            // CombatInteraction(Dead) + EntityDied so the dinosaur lies down instead of standing!
                            let dead_mv = make_animal_pose_move(
                                &target_id,
                                &dead_motion,
                                ax,
                                ay,
                                ayaw,
                                now,
                                30.0,
                                false,
                            );
                            let dead_mv_frame = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_MOVE,
                                &dead_mv.pack()?,
                            )?;
                            state.broadcast_region_all(region_id, &dead_mv_frame).await;

                            let dead_ci = build_combat_interaction_msg(
                                &target_id,
                                &eid,
                                ANIMAL_STATUS_DEAD,
                                false,
                                None,
                            );
                            let dead_ci_frame = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_COMBAT_INTERACTION,
                                &dead_ci.pack()?,
                            )?;
                            state.broadcast_region_all(region_id, &dead_ci_frame).await;

                            let died_msg = EntityDiedMsg {
                                entity_id: target_id.clone(),
                                time: now,
                            };
                    state.db.log_audit(
                                now,
                                "COMBAT",
                                &authed_name,
                                &peer_ip,
                                region_id,
                                &format!("Defeated wildlife {}", target_id),
                            );
                            let died_frame = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ENTITY_DIED,
                                &died_msg.pack()?,
                            )?;
                            state.broadcast_region_all(region_id, &died_frame).await;

                            let aggroed_players: std::collections::HashSet<String> = {
                                let animals = state.animals.read().await;
                                animals
                                    .values()
                                    .filter(|a| a.is_alive && a.region_id == region_id)
                                    .filter_map(|a| a.enemy_id.clone())
                                    .collect()
                            };
                            let mut ended_recipients: Vec<(String, tokio::sync::mpsc::Sender<Vec<u8>>)> = Vec::new();
                            {
                                let mut live = state.live_clients.write().await;
                                for (cid, cl) in live.iter_mut() {
                                    if cl.region_id == region_id
                                        && cl.in_battle
                                        && (cid == &eid
                                            || cl.battle_target_id.as_deref() == Some(target_id.as_str()))
                                        && !aggroed_players.contains(cid)
                                    {
                                        cl.in_battle = false;
                                        cl.battle_target_id = None;
                                        ended_recipients.push((cid.clone(), cl.tx.clone()));
                                    }
                                }
                            }
                            for (cid, peer_tx) in ended_recipients {
                                let ended = BattleEndedMsg {
                                    entity_id: cid,
                                    time: now,
                                };
                                let end_frame = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_BATTLE_ENDED,
                                    &ended.pack()?,
                                )?;
                                let _ = peer_tx.try_send(end_frame);
                            }

                            let kill_skill_cat = if is_ranged_action {
                                SKILL_CAT_RANGED
                            } else {
                                SKILL_CAT_MELEE
                            };
                            award_xp(
                                &state,
                                &tx,
                                &next_seq,
                                &eid,
                                60,
                                Some((kill_skill_cat, 35)),
                            )
                            .await;
                        }
                    }
                }
                TC_GET_STATISTICS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let ch = state.resolve_character(&eid).await;
                    let stats = StatisticsMsg {
                        level: ch.as_ref().map(|c| c.level.max(1)).unwrap_or(1),
                        exp: ch.as_ref().map(|c| c.exp.max(0)).unwrap_or(0),
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_STATISTICS,
                        &stats.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_SKILLS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let skills = build_skills_msg(&state, &eid).await;
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_SKILLS,
                        &skills.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_LEARN_SKILL => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = LearnSkillMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let norm_sub = if req.sub_id.is_empty() {
                        "__base__".to_string()
                    } else {
                        req.sub_id.clone()
                    };
                    if req.level < 1 {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    }
                    let target_level = req.level;
                    let spec = if state.catalog.has_loaded_skills() {
                        match state.catalog.find_skill_node(&req.skill_id, &norm_sub, target_level) {
                            Some(s) => s,
                            None => {
                                let fail = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    h.seq,
                                    TC_FAILED,
                                    &pack_empty_struct()?,
                                )?;
                                let _ = tx.send(fail).await;
                                continue;
                            }
                        }
                    } else {
                        state
                            .catalog
                            .resolve_skill_node(&req.skill_id, &norm_sub, target_level)
                    };
                    let (current, remaining_sp) = build_skills_state(&state, &eid).await;
                    let cur_lv = current
                        .skill_list
                        .iter()
                        .find(|b| b.skill_id == req.skill_id)
                        .and_then(|b| b.levels.get(&norm_sub).copied())
                        .unwrap_or(0);
                    let player_cat_lv = current
                        .categories
                        .get(&spec.category)
                        .map(|c| c.level)
                        .unwrap_or(1);
                    let needed_sp = spec.skill_point.max(0);
                    if target_level != cur_lv + 1
                        || remaining_sp < needed_sp
                        || (state.catalog.has_loaded_skills()
                            && player_cat_lv < spec.category_level)
                    {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    }
                    let _ = state.db.upsert_player_skill(
                        &eid,
                        spec.category,
                        &req.skill_id,
                        &norm_sub,
                        target_level,
                    );
                    state.db.log_audit(
                        now,
                        "SKILL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!(
                            "Learned skill '{}:{}' Lv.{} ({} SP)",
                            req.skill_id, norm_sub, target_level, needed_sp
                        ),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let updated = build_skills_msg(&state, &eid).await;
                    let sk_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SKILLS,
                        &updated.pack()?,
                    )?;
                    let _ = tx.send(sk_frame).await;

                    let ch = state.resolve_character(&eid).await;
                    let st_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_STATISTICS,
                        &StatisticsMsg {
                            level: ch.as_ref().map(|c| c.level.max(1)).unwrap_or(1),
                            exp: ch.as_ref().map(|c| c.exp.max(0)).unwrap_or(0),
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(st_frame).await;
                    push_recipes_and_blueprints(&state, &tx, &next_seq, &eid, now).await;
                }
                TC_UNTRAIN_SKILL => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = UntrainSkillMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let norm_sub = if req.sub_id.is_empty() {
                        "__base__".to_string()
                    } else {
                        req.sub_id.clone()
                    };
                    let learned = state.db.list_player_skills(&eid).unwrap_or_default();
                    let existing = learned
                        .into_iter()
                        .find(|s| s.skill_id == req.skill_id && s.sub_id == norm_sub);
                    let Some(existing) = existing else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    let spec = state.catalog.resolve_skill_node(
                        &existing.skill_id,
                        &existing.sub_id,
                        existing.level,
                    );
                    if spec.untrain_disabled || spec.skill_point == 0 {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    }
                    let next_lv = (existing.level - 1).max(0);
                    let _ = state.db.upsert_player_skill(
                        &eid,
                        existing.category,
                        &existing.skill_id,
                        &existing.sub_id,
                        next_lv,
                    );
                    let _ = state.db.increment_untrained_count(&eid);
                    if let Some(tmp) = state.temp_characters.write().await.get_mut(&eid) {
                        tmp.untrained_count += 1;
                    }
                    state.db.log_audit(
                        now,
                        "SKILL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!(
                            "Untrained skill '{}:{}' to Lv.{}",
                            existing.skill_id, existing.sub_id, next_lv
                        ),
                    );

                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let updated = build_skills_msg(&state, &eid).await;
                    let sk_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SKILLS,
                        &updated.pack()?,
                    )?;
                    let _ = tx.send(sk_frame).await;
                    push_recipes_and_blueprints(&state, &tx, &next_seq, &eid, now).await;
                }
                TC_RESEARCH_SKILL_CATEGORY => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = ResearchSkillCategoryMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !ALL_SKILL_CATEGORIES.contains(&req.category) {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    }
                    let char_lv = state
                        .resolve_character(&eid)
                        .await
                        .map(|c| c.level.max(1))
                        .unwrap_or(1);
                    let stored = state.db.list_skill_categories(&eid).unwrap_or_default();
                    let (cur_lv, cur_xp) = stored.get(&req.category).copied().unwrap_or((1, 0));
                    let cap_lv = char_lv.min(60);
                    if cur_lv >= cap_lv {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    }
                    // Zero-paywall preservation: category research completes immediately at no cost up to char_lv
                    let next_lv = (cur_lv + 1).min(cap_lv);
                    let _ = state
                        .db
                        .upsert_skill_category(&eid, req.category, next_lv, cur_xp);
                    state.db.log_audit(
                        now,
                        "SKILL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!("Researched skill category #{} to Lv.{}", req.category, next_lv),
                    );
                    ensure_starter_skills(&state, &eid).await;

                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let updated = build_skills_msg(&state, &eid).await;
                    let sk_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SKILLS,
                        &updated.pack()?,
                    )?;
                    let _ = tx.send(sk_frame).await;
                    push_recipes_and_blueprints(&state, &tx, &next_seq, &eid, now).await;
                }
                TC_GET_QUEST_CATEGORIES => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_QUEST_CATEGORIES,
                        &pack_empty_quest_categories()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_QUESTS => {
                    if !is_ready {
                        continue;
                    }
                    let req = GetQuestsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let resp = QuestsMsg {
                        category: req.category,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_QUESTS,
                        &resp.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_PETS_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let pets = PetsInfoMsg { grazable_count: 0 };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PETS_INFO,
                        &pets.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ATTACHABLE_ACCESSORIES => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    // Global handler in EquipSystem.cs expects ReplyOf = 0
                    let acc = AttachableAccessoriesMsg {
                        accessories: Vec::new(),
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ATTACHABLE_ACCESSORIES,
                        &acc.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_RESISTANCE_EXP_CAPS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_RESISTANCE_EXP_CAPS,
                        &pack_empty_resistance_exp_caps()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ESTATE_LICENSES => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ESTATE_LICENSES,
                        &build_estate_licenses_msg(&state, &eid).pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_AVAILABLE_EMOTIONS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let emo = AvailableEmotionsMsg {
                        motions: Vec::new(),
                        emoticons: Vec::new(),
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_AVAILABLE_EMOTIONS,
                        &emo.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_CHEAT_FLAGS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let cf = CheatFlagsMsg {
                        flags: BTreeMap::new(),
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_CHEAT_FLAGS,
                        &cf.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_REGION_MAP_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let req = GetRegionMapInfoMsg::unpack(&frame.msgpack)?;
                    let terrain = state.terrains.get_or_load(&req.region_id).await;
                    let mut all_chunks =
                        Vec::with_capacity(terrain.num_chunks_x() * terrain.num_chunks_y());
                    for cx in 0..terrain.num_chunks_x() {
                        for cy in 0..terrain.num_chunks_y() {
                            all_chunks.push((cx as u8, cy as u8));
                        }
                    }
                    let info = RegionMapInfoMsg {
                        region_id: req.region_id,
                        terrain_id: terrain.id.clone(),
                        tile_count_x: terrain.width.min(u16::MAX as usize) as u16,
                        tile_count_y: terrain.height.min(u16::MAX as usize) as u16,
                        defogged_chunks: all_chunks,
                    };
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REGION_MAP_INFO,
                        &info.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_IS_WARPHOLE_AVAILABLE => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_WARP_COSTS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WARP_COSTS,
                        &pack_empty_warp_costs()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_WARP => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = WarpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot warp while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE).to_string();
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let tx_clamped = req.tile_x.min((terrain.width.saturating_sub(1)) as u16);
                    let ty_clamped = req.tile_y.min((terrain.height.saturating_sub(1)) as u16);
                    if !terrain.is_dry_land(tx_clamped, ty_clamped) {
                        let abort = AbortMsg {
                            text: "Destination tile is in water.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if let Ok(Some(estate)) = state.db.find_active_estate_for_tile(
                        &region_id,
                        tx_clamped,
                        ty_clamped,
                        1,
                        1,
                        now,
                    ) {
                        if estate.access_for_others == 0
                            && !is_estate_owner(&state, &eid, &estate)
                            && !state.is_admin_owner(&authed_owner)
                        {
                            let abort = AbortMsg {
                                text: "Cannot warp into a private domain.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    }

                    // Cancel active gather & combat/aggro before warping
                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    {
                        let mut animals = state.animals.write().await;
                        for a in animals.values_mut() {
                            if a.enemy_id.as_deref() == Some(eid.as_str()) {
                                a.enemy_id = None;
                            }
                        }
                    }

                    let warp_dur = if state.config.collect_delay_ms < 200 {
                        0.05
                    } else {
                        0.3
                    };
                    state.db.log_audit(
                        ServerState::now_unix_secs(),
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Warped to tile ({tx_clamped}, {ty_clamped})"),
                    );
                    start_warp_sequence(
                        state.clone(),
                        tx.clone(),
                        next_seq.clone(),
                        eid,
                        conn_id,
                        region_id,
                        h.seq,
                        (tx_clamped, ty_clamped),
                        TELEPORT_TYPE_WARP,
                        warp_dur,
                    )
                    .await?;
                }
                TC_REGISTER_PRODUCT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = RegisterProductMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !req.duration.is_finite()
                        || req.duration <= 0.0
                        || req.price <= 0
                        || req.price > 100_000_000
                    {
                        let abort = AbortMsg {
                            text: "Invalid market price or duration.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let region_id = authed_region
                        .clone()
                        .unwrap_or_else(|| crate::state::DEFAULT_REGION_CODE.to_string());
                    let listed = state
                        .db
                        .register_market_products(
                            &eid,
                            &region_id,
                            std::slice::from_ref(&req.item_id),
                            req.price,
                            req.duration,
                            50,
                            now,
                        )
                        .unwrap_or_default();
                    let Some((removed_id, item_name)) = listed.into_iter().next() else {
                        let abort = AbortMsg {
                            text: "Cannot list item (missing, locked, equipped, or 50-listing cap reached).".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    state.db.log_audit(
                        now,
                        "MARKET",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Listed {} for {} T-Stone", item_name, req.price),
                    );

                    let inv_upd = InventoryUpdatedMsg {
                        entity_id: eid,
                        items: Vec::new(),
                        removed_item_ids: vec![removed_id],
                        item_order: None,
                    };
                    let upd_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY_UPDATED,
                        &inv_upd.pack()?,
                    )?;
                    let _ = tx.send(upd_frame).await;

                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_SEARCH_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let req = SearchProductsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let found = state
                        .db
                        .search_market_products(
                            now,
                            req.item_name.as_deref(),
                            req.prototype_id.as_deref(),
                            req.skip.max(0) as usize,
                        )
                        .unwrap_or_default();
                    let products: Vec<ProductMsg> = found
                        .into_iter()
                        .map(|p| {
                            let lvl = p.item.level;
                            let dur = p.item.durability;
                            let im = item_record_to_msg(&p.item, &state.catalog, now);
                            ProductMsg {
                                id: p.product_id,
                                region_id: p.region_id,
                                listed_at: p.listed_at,
                                expires_at: p.expires_at,
                                deletes_at: p.expires_at + 86_400.0,
                                price: p.price,
                                items: vec![im],
                                level: lvl,
                                durability: dur,
                            }
                        })
                        .collect();
                    let resp = ProductsMsg { products };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PRODUCTS,
                        &resp.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_BUY_PRODUCT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = BuyProductMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let (bought_item, prod, new_buyer_bal) = match state
                        .db
                        .buy_market_product(&eid, &req.product_id, MAX_INVENTORY_ITEMS, now)
                    {
                        Ok(Ok(res)) => res,
                        Ok(Err(reason)) => {
                            let abort = AbortMsg {
                                text: reason.into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                        Err(_) => {
                            let abort = AbortMsg {
                                text: "Market purchase failed due to database error.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    };
                    state.db.log_audit(
                        now,
                        "MARKET",
                        &authed_name,
                        &peer_ip,
                        &prod.region_id,
                        &format!("Purchased {} for {} T-Stone", bought_item.name, prod.price.max(0)),
                    );
                    let item_msg = item_record_to_msg(&bought_item, &state.catalog, now);

                    let inv_upd = InventoryUpdatedMsg {
                        entity_id: eid.clone(),
                        items: vec![item_msg],
                        removed_item_ids: Vec::new(),
                        item_order: None,
                    };
                    let upd_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY_UPDATED,
                        &inv_upd.pack()?,
                    )?;
                    let _ = tx.send(upd_frame).await;

                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    if prod.price > 0 {
                        let wf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_WALLET_UPDATED,
                            &WalletUpdatedMsg {
                                entity_id: eid,
                                t_stone: new_buyer_bal,
                            }
                            .pack()?,
                        )?;
                        let _ = tx.send(wf).await;
                    }
                }
                TC_RECOMMEND_STABLE_REGIONS => {
                    if !is_ready {
                        continue;
                    }
                    let current = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let others: Vec<String> = state
                        .terrains
                        .list_available_terrains()
                        .into_iter()
                        .map(|tid| crate::state::format_region_code(&tid))
                        .filter(|code| code != current)
                        .collect();
                    let now = ServerState::now_unix_secs();
                    let resp = RecommendedStableRegionsMsg { region_ids: others };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_RECOMMENDED_STABLE_REGIONS,
                        &resp.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ROUTE_OF_ARCHIPELAGO => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let msg = RoutesOfArchipelagoMsg {
                        archipelago_routes: build_archipelago_routes(&state),
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ROUTES_OF_ARCHIPELAGO,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_SAILING_BACK_COST => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_SAILING_BACK_COST,
                        &SailingBackCostMsg { cost: 0 }.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_RECOMMEND_REGION => {
                    if !is_ready {
                        continue;
                    }
                    let req = RecommendRegionMsg::unpack(&frame.msgpack)?;
                    let target_code = if let Some(tpl) = req.template_id.as_deref().filter(|s| !s.is_empty()) {
                        crate::state::format_region_code(tpl)
                    } else if req.role == RegionRole::Personal as i32 {
                        crate::state::DEFAULT_REGION_CODE.to_string()
                    } else {
                        "unstable-savanna-lv15-01".to_string()
                    };
                    let region = build_region_msg(&state, &target_code).await;
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REGION,
                        &region.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_RECOMMEND_ARCHIPELAGO => {
                    if !is_ready {
                        continue;
                    }
                    let req = RecommendArchipelagoMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let matched = crate::state::KNOWN_REGION_TEMPLATES
                        .iter()
                        .find(|m| {
                            m.role == RegionRole::Risky
                                && m.level == req.level
                                && (m.biome == req.biome || (req.level == 20 && req.biome == 0))
                        })
                        .or_else(|| {
                            crate::state::KNOWN_REGION_TEMPLATES
                                .iter()
                                .find(|m| m.role == RegionRole::Risky && m.level == req.level)
                        })
                        .map(|m| m.zone_code)
                        .unwrap_or("unstable-savanna-lv15-01");
                    let msg = build_archipelago_msg(matched, now);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ARCHIPELAGO,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_TRAVEL_TO_STABLE_REGION
                | TC_RETURN_TO_CAMP
                | TC_TRAVEL_BY_REGION
                | TC_TRAVEL_BY_REGION_IN_ARCHIPELAGO
                | TC_WITHDRAW
                | TC_SAILING_BACK
                | TC_WARP_TO_PERSONAL_REGION
                | TC_TRAVEL_TO_RANDOM_PERSONAL_REGION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let requested = match h.type_code {
                        TC_RETURN_TO_CAMP
                        | TC_WITHDRAW
                        | TC_SAILING_BACK
                        | TC_WARP_TO_PERSONAL_REGION => {
                            let pts = ensure_personal_region_points(&state, &eid, &cur_region);
                            pts.personal_region_id
                                .unwrap_or_else(|| crate::state::DEFAULT_REGION_CODE.to_string())
                        }
                        TC_TRAVEL_TO_RANDOM_PERSONAL_REGION => {
                            let candidates = [
                                "tamed-grassland-lv10-01",
                                "tamed-grassland-lv10-02",
                                "tamed-grassland-lv10-03",
                                "tamed-grassland-lv10-04",
                                "tamed-grassland-lv10-05",
                            ];
                            let idx = ((ServerState::now_unix_secs() * 1000.0) as usize)
                                % candidates.len();
                            let pick = candidates[idx];
                            if pick == cur_region {
                                candidates[(idx + 1) % candidates.len()].to_string()
                            } else {
                                pick.to_string()
                            }
                        }
                        TC_TRAVEL_BY_REGION => {
                            let req = TravelByRegionMsg::unpack(&frame.msgpack)?;
                            if req.region_id.is_empty() {
                                crate::state::DEFAULT_REGION_CODE.to_string()
                            } else {
                                req.region_id
                            }
                        }
                        TC_TRAVEL_BY_REGION_IN_ARCHIPELAGO => {
                            let req = TravelByRegionInArchipelagoMsg::unpack(&frame.msgpack)?;
                            if req.region_id.is_empty() {
                                "unstable-savanna-lv15-01".to_string()
                            } else {
                                req.region_id
                            }
                        }
                        _ => {
                            let req = TravelToStableRegionMsg::unpack(&frame.msgpack)?;
                            if req.region_id.is_empty() {
                                crate::state::DEFAULT_REGION_CODE.to_string()
                            } else {
                                req.region_id
                            }
                        }
                    };
                    let target_terrain = crate::state::terrain_id_for_region(&requested);
                    let target_region = crate::state::format_region_code(&target_terrain);
                    let available = state.terrains.list_available_terrains();
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot sail while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if !available.is_empty() && !available.iter().any(|id| id == &target_terrain) {
                        let abort = AbortMsg {
                            text: format!("Destination island '{requested}' not found."),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    // Cancel in-flight gather, craft, combat & aggro before emigrating so disconnect
                    // won't overwrite the new island's (0.0, 0.0) entry spawn!
                    emigrated = true;
                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    active_craft_gen.fetch_add(1, Ordering::Relaxed);
                    if let Some(cancel_tx) = active_craft_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    loaded_chunks.clear();
                    touched_naturals.clear();
                    visible_artifacts.clear();
                    let surv_flush = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).map(|client| {
                            client.stop_rest(now);
                            client.region_id = target_region.clone();
                            client.pos_x = 0.0;
                            client.pos_y = 0.0;
                            client.in_battle = false;
                            client.battle_target_id = None;
                            client.boarding_on = BOARDING_ON_NONE;
                            client.boarding_since = None;
                            client.boarding_until = None;
                            client.vehicle_entity_id.clear();
                            client.warp_generation = client.warp_generation.wrapping_add(1);
                            (
                                client.life,
                                client.current_stamina(now),
                                client.energy,
                                client.fatigue,
                            )
                        })
                    };
                    if let Some((life, stam, energy, fatigue)) = surv_flush {
                        let _ = state.db.update_survival(&eid, life, stam, energy, fatigue);
                    }
                    {
                        let mut animals = state.animals.write().await;
                        for a in animals.values_mut() {
                            if a.enemy_id.as_deref() == Some(eid.as_str()) {
                                a.enemy_id = None;
                            }
                        }
                    }
                    let mut pts = ensure_personal_region_points(&state, &eid, &cur_region);
                    if pts.returning_region_id.as_deref() != Some(cur_region.as_str()) {
                        pts.returning_x = None;
                        pts.returning_y = None;
                    }
                    pts.returning_region_id = Some(cur_region.clone());
                    let _ = state.db.upsert_player_points(&eid, &pts);
                    let _ = state
                        .db
                        .update_character_region(&eid, &target_region, &target_terrain);
                    if let Some(tmp) = state.temp_characters.write().await.get_mut(&eid) {
                        tmp.region_id = target_region.clone();
                        tmp.terrain_id = target_terrain.clone();
                        tmp.pos_x = 0.0;
                        tmp.pos_y = 0.0;
                    }
                    state.db.log_audit(
                        now,
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        &target_region,
                        &format!("Sailed from {} to {}", cur_region, target_region),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let em = EmigratedMsg { teleport_type: 0 };
                    let em_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_EMIGRATED,
                        &em.pack()?,
                    )?;
                    let _ = tx.send(em_frame).await;
                }
                TC_OPEN_MAP => {
                    if !is_ready {
                        continue;
                    }
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let mut all_chunks =
                        Vec::with_capacity(terrain.num_chunks_x() * terrain.num_chunks_y());
                    for cx in 0..terrain.num_chunks_x() {
                        for cy in 0..terrain.num_chunks_y() {
                            all_chunks.push((cx as u8, cy as u8));
                        }
                    }
                    let defog = DefoggedChunks { chunks: all_chunks };
                    let defog_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_DEFOGGED_CHUNKS,
                        &defog.pack()?,
                    )?;
                    let _ = tx.send(defog_frame).await;
                }
                TC_MAKE_CLAN => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = MakeClanMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cname = req.clan_name.trim().to_string();
                    let cname_chars = cname.chars().count();
                    if cname_chars < 2 || cname_chars > 16 || cname.chars().any(char::is_control) {
                        let abort = AbortMsg {
                            text: "Clan name must be between 2 and 16 characters.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if state.db.get_player_clan(&eid).ok().flatten().is_some() {
                        let abort = AbortMsg {
                            text: "You are already in a clan.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let clan_id = Uuid::new_v4().to_string();
                    if state.db.create_clan(&clan_id, &cname, &eid, now).is_err() {
                        let abort = AbortMsg {
                            text: "Clan name is already taken.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    state.db.log_audit(
                        now,
                        "SOCIAL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!("Created clan '{cname}'"),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let member = MemberMsg {
                        entity_id: eid,
                        clan_id,
                        clan_name: cname,
                        role_id: 1,
                        applying_clan_id: None,
                    };
                    let mem_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_MEMBER,
                        &member.pack()?,
                    )?;
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    state.broadcast_region_all(region_id, &mem_frame).await;
                }
                TC_LEAVE_CLAN => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let _req = LeaveClanMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !state.db.leave_clan(&eid).unwrap_or(false) {
                        let abort = AbortMsg {
                            text: "You are not currently in a clan.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    state.db.log_audit(
                        now,
                        "SOCIAL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        "Left clan",
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;

                    let member = MemberMsg {
                        entity_id: eid,
                        clan_id: String::new(),
                        clan_name: String::new(),
                        role_id: 0,
                        applying_clan_id: None,
                    };
                    let mem_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_MEMBER,
                        &member.pack()?,
                    )?;
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    state.broadcast_region_all(region_id, &mem_frame).await;
                }
                TC_GET_REVIVE_IMMEDIATELY_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    // Zero-paywall preservation: immediate revive is always free
                    let info = ReviveImmediatelyInfoMsg {
                        used_count: 0,
                        cost_amount: 0,
                        cost_currency: 0,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REVIVE_IMMEDIATELY_INFO,
                        &info.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_REVIVE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = ReviveMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE).to_string();
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let (raw_tx, raw_ty) = req.warphole_tile.unwrap_or(terrain.entry_tile);
                    let mut tile_x = raw_tx.min(terrain.width.saturating_sub(1) as u16);
                    let mut tile_y = raw_ty.min(terrain.height.saturating_sub(1) as u16);
                    if !terrain.is_dry_land(tile_x, tile_y) {
                        tile_x = terrain.entry_tile.0.min(terrain.width.saturating_sub(1) as u16);
                        tile_y = terrain.entry_tile.1.min(terrain.height.saturating_sub(1) as u16);
                    }
                    let wx = f32::from(tile_x) * 200.0;
                    let wy = f32::from(tile_y) * 200.0;

                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }

                    let currently_alive = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .is_some_and(|c| c.is_alive);
                    if currently_alive {
                        let abort = AbortMsg {
                            text: "You do not need to revive while alive.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let surv_snap = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).map(|c| {
                            c.life = 70.0;
                            c.stamina = 70.0;
                            c.stamina_at = now;
                            c.energy = 70.0;
                            c.fatigue = 0.0;
                            c.is_alive = true;
                            c.in_battle = false;
                            c.battle_target_id = None;
                            c.last_battle_action_at = 0.0;
                            c.pos_x = wx;
                            c.pos_y = wy;
                            c.to_survival_msg(now)
                        })
                    };
                    let _ = state.db.update_position(&eid, wx, wy, now);
                    let _ = state.db.update_survival(&eid, 70.0, 70.0, 70.0, 0.0);
                    state.db.log_audit(
                        now,
                        "SURVIVAL",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Revived at ({tile_x}, {tile_y})"),
                    );

                    if let Some(surv) = surv_snap {
                        let surv_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &surv.pack()?,
                        )?;
                        let _ = tx.send(surv_frame).await;
                    }

                    let tp_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_TELEPORTED,
                        &TeleportedMsg {
                            tile_x,
                            tile_y,
                            teleport_type: TELEPORT_TYPE_REVIVE,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(tp_frame).await;

                    let peer_mv = make_animal_pose_move(
                        &eid,
                        "NormalIdle",
                        wx,
                        wy,
                        0.0,
                        now,
                        0.1,
                        true,
                    );
                    if let Ok(mv_packed) = peer_mv.pack() {
                        if let Ok(mv_frame) = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_MOVE,
                            &mv_packed,
                        ) {
                            state.broadcast_region_except(&region_id, &eid, &mv_frame).await;
                        }
                    }

                    let rev_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REVIVED,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(rev_frame).await;

                    let erev = EntityRevivedMsg {
                        entity_id: eid,
                        at: now,
                    };
                    let erev_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ENTITY_REVIVED,
                        &erev.pack()?,
                    )?;
                    state.broadcast_region_all(&region_id, &erev_frame).await;
                }
                TC_REVIVE_IMMEDIATELY => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let _req = ReviveImmediatelyMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE).to_string();

                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }

                    let currently_alive = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .is_some_and(|c| c.is_alive);
                    if currently_alive {
                        let abort = AbortMsg {
                            text: "You do not need to revive while alive.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let surv_snap = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).map(|c| {
                            c.life = 100.0;
                            c.stamina = 100.0;
                            c.stamina_at = now;
                            c.energy = 100.0;
                            c.fatigue = 0.0;
                            c.is_alive = true;
                            c.in_battle = false;
                            c.battle_target_id = None;
                            c.last_battle_action_at = 0.0;
                            c.to_survival_msg(now)
                        })
                    };
                    let _ = state.db.update_survival(&eid, 100.0, 100.0, 100.0, 0.0);
                    state.db.log_audit(
                        now,
                        "SURVIVAL",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        "Revived immediately in place",
                    );

                    if let Some(surv) = surv_snap {
                        let surv_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &surv.pack()?,
                        )?;
                        let _ = tx.send(surv_frame).await;
                    }

                    let rev_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REVIVED,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(rev_frame).await;

                    let erev = EntityRevivedMsg {
                        entity_id: eid,
                        at: now,
                    };
                    let erev_frame = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ENTITY_REVIVED,
                        &erev.pack()?,
                    )?;
                    state.broadcast_region_all(&region_id, &erev_frame).await;
                }
                TC_KEEPALIVE => {
                    // Heartbeat keepalive — acknowledge quietly so idle sessions stay alive.
                }
                TC_GET_RECIPES => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    ensure_starter_skills(&state, &eid).await;
                    let (recipes, _) = build_recipes_and_blueprints(&state, &eid);
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_RECIPES,
                        &recipes.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ARTIFACT_BLUEPRINTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    ensure_starter_skills(&state, &eid).await;
                    let (_, blueprints) = build_recipes_and_blueprints(&state, &eid);
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ARTIFACT_BLUEPRINTS,
                        &blueprints.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_ESTIMATE_BUILD => {
                    if !is_ready {
                        continue;
                    }
                    let req = EstimateBuildMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let (sx, sy, rot, lv) = state
                        .db
                        .get_artifact(&req.entity_id)
                        .ok()
                        .flatten()
                        .map(|a| (a.size_x, a.size_y, a.rotation, i32::from(a.level.max(1))))
                        .unwrap_or((1, 1, 0, 1));
                    let est = BuildEstimationMsg {
                        entity_id: req.entity_id,
                        level: lv,
                        durability: 100.0,
                        tags: BTreeMap::new(),
                        unrevealed_rare_tag_count: 0,
                        size_x: sx,
                        size_y: sy,
                        rotation: rot,
                        is_modular: false,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_BUILD_ESTIMATION,
                        &est.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ARTIFACT => {
                    if !is_ready {
                        continue;
                    }
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if let Some(art) = state
                        .db
                        .get_artifact(&req.entity_id)
                        .ok()
                        .flatten()
                        .filter(|a| a.region_id == cur_region)
                    {
                        let mut materials: BTreeMap<String, Vec<ItemMsg>> = BTreeMap::new();
                        if let Ok(stored) = serde_json::from_str::<BTreeMap<String, Vec<ItemRecord>>>(
                            &art.materials_json,
                        ) {
                            for (slot, recs) in stored {
                                materials.insert(
                                    slot,
                                    recs.iter()
                                        .map(|r| item_record_to_msg(r, &state.catalog, now))
                                        .collect(),
                                );
                            }
                        }
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ARTIFACT_MATERIALS,
                            &ArtifactMaterialsMsg {
                                entity_id: art.entity_id,
                                materials,
                            }
                            .pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                    } else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                    }
                }
                TC_GET_WORKBENCH => {
                    if !is_ready {
                        continue;
                    }
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WORKBENCH,
                        &WorkbenchMsg {
                            entity_id: req.entity_id,
                            capacity: 4,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_CANCEL_CRAFTING => {
                    if !is_ready {
                        continue;
                    }
                    let _req = CancelCraftingMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    active_craft_gen.fetch_add(1, Ordering::Relaxed);
                    if let Some(cancel_tx) = active_craft_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_SET_STORAGE_ITEM => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    match SetStorageItemMsg::unpack(&frame.msgpack) {
                        Ok(req) => {
                            if !req.key.is_empty() && req.value.len() <= 65_536 {
                                if let Err(err) = state.db.set_player_storage(&eid, &req.key, &req.value) {
                                    tracing::warn!(%peer, entity = %eid, key = %req.key, %err, "failed to persist SetStorageItem");
                                } else if req.key == "personal_guide_progress" || req.key == "common_guide_progress" {
                                    tracing::info!(%peer, entity = %eid, key = %req.key, bytes = req.value.len(), "TCP SetStorageItem persisted PlayGuide progress");
                                }
                            }
                        }
                        Err(err) => {
                            tracing::warn!(%peer, entity = %eid, %err, "failed to decode SetStorageItem");
                        }
                    }
                }
                TC_GET_PIONEER_GRADE_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let p_level = state
                        .resolve_character(&eid)
                        .await
                        .map_or(1, |c| c.level.max(1));
                    let p_grade = ((p_level - 1) / 7 + 1).clamp(1, 9);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PIONEER_GRADE_INFO,
                        &PioneerGradeInfoMsg {
                            entity_id: eid,
                            grade: p_grade,
                            point: 0.0,
                            point_needed: 100 * p_grade,
                            current_maximum_estate_size: 10 + p_grade * 5,
                            current_access_level: p_grade,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_DISCOVERY_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_DISCOVERY_INFO,
                        &DiscoveryInfoMsg {
                            template_id: req.value,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_PERSONAL_REGION_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let info = build_personal_region_info_msg(&state, &eid, region_id).await;
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PERSONAL_REGION_INFO,
                        &info.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ESTATE_LICENSE_BY_ID => {
                    if !is_ready {
                        continue;
                    }
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if let Some(rec) = state.db.get_estate_by_id(&req.value).ok().flatten() {
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ESTATE_LICENSE,
                            &estate_record_to_msg(&rec).pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                    } else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                    }
                }
                TC_DECLARE_ESTATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = DeclareEstateMsg::unpack(&frame.msgpack)?;
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let now = ServerState::now_unix_secs();
                    let _ = state.db.purge_expired_estates(&region_id, now);
                    let owner_type = if req.owner_type == OWNER_TYPE_CLAN_ESTATE {
                        OWNER_TYPE_CLAN_ESTATE
                    } else if region_role_for(&region_id) == RegionRole::Personal
                        || req.owner_type == OWNER_TYPE_PERSONAL_PLAYER
                    {
                        OWNER_TYPE_PERSONAL_PLAYER
                    } else {
                        OWNER_TYPE_PLAYER
                    };
                    let effective_owner_id = if owner_type == OWNER_TYPE_CLAN_ESTATE {
                        let Some((clan_id, _, _)) = state.db.get_player_clan(&eid).ok().flatten() else {
                            let abort = AbortMsg {
                                text: "Join a clan before claiming a clan domain.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        };
                        clan_id
                    } else {
                        eid.clone()
                    };
                    let existing = state
                        .db
                        .list_estates_by_owner(&effective_owner_id)
                        .unwrap_or_default();
                    if existing
                        .iter()
                        .any(|e| e.owner_type == owner_type && e.expires_at > now)
                    {
                        let abort = AbortMsg {
                            text: "You already own a domain of this type.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let max_cx = terrain.width.div_ceil(4) as u8;
                    let max_cy = terrain.height.div_ceil(4) as u8;
                    if req.cell_x >= max_cx || req.cell_y >= max_cy {
                        let abort = AbortMsg {
                            text: "Domain cell is outside island bounds.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let base_tx = u16::from(req.cell_x) * 4;
                    let base_ty = u16::from(req.cell_y) * 4;
                    let has_dry_land = (0u16..4).any(|dx| {
                        (0u16..4).any(|dy| terrain.is_dry_land(base_tx + dx, base_ty + dy))
                    });
                    if !has_dry_land {
                        let abort = AbortMsg {
                            text: "Cannot claim domain on open water.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let cell = (req.cell_x, req.cell_y);
                    let rec = EstateRecord {
                        estate_id: Uuid::new_v4().to_string(),
                        owner_type,
                        owner_id: effective_owner_id,
                        region_id: region_id.clone(),
                        anchor_x: base_tx + 2,
                        anchor_y: base_ty + 2,
                        activated_at: now,
                        expires_at: now + ESTATE_LEASE_DAYS * 86_400.0,
                        access_for_others: 1,
                        cells: vec![cell],
                    };
                    if state.db.upsert_estate(&rec).is_err() {
                        let abort = AbortMsg {
                            text: "Domain cell is already claimed by another survivor.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let _ = state.db.set_player_storage(&eid, "_balloon_landed", b"1");
                    state.db.log_audit(
                        now,
                        "DOMAIN",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Claimed domain cell ({}, {})", req.cell_x, req.cell_y),
                    );
                    let lic = estate_record_to_msg(&rec);
                    let reply = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ESTATE_LICENSE,
                        &lic.pack()?,
                    )?;
                    let _ = tx.send(reply).await;
                    let chunk_coord = (req.cell_x / 4, req.cell_y / 4);
                    if let Some(grids) =
                        build_estate_grids_for_region(&state, &region_id, vec![chunk_coord])
                    {
                        let gf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_ESTATE_GRIDS,
                            &grids.pack()?,
                        )?;
                        state.broadcast_region_all(&region_id, &gf).await;
                    }
                    let el = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_LICENSES,
                        &build_estate_licenses_msg(&state, &eid).pack()?,
                    )?;
                    let _ = tx.send(el).await;
                }
                TC_EXPAND_ESTATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EstateCellOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let Some(mut rec) = state.db.get_estate_by_id(&req.estate_id).ok().flatten()
                    else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    let _ = state.db.purge_expired_estates(&rec.region_id, now);
                    if rec.expires_at <= now || !is_estate_owner(&state, &eid, &rec) {
                        let abort = AbortMsg {
                            text: "Cannot expand this domain.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let p_level = state
                        .resolve_character(&eid)
                        .await
                        .map_or(1, |c| c.level.max(1));
                    let p_grade = ((p_level - 1) / 7 + 1).clamp(1, 9);
                    let max_cells = (10 + p_grade * 5) as usize;
                    if rec.cells.len() >= max_cells {
                        let abort = AbortMsg {
                            text: "Cannot expand domain beyond limit.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let terrain = state.terrains.get_or_load(&rec.region_id).await;
                    let max_cx = terrain.width.div_ceil(4) as u8;
                    let max_cy = terrain.height.div_ceil(4) as u8;
                    if req.cell_x >= max_cx || req.cell_y >= max_cy {
                        let abort = AbortMsg {
                            text: "Domain cell is outside island bounds.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let cell = (req.cell_x, req.cell_y);
                    if !rec.cells.contains(&cell) {
                        let is_adjacent = rec.cells.iter().any(|&(cx, cy)| {
                            cx.abs_diff(req.cell_x) + cy.abs_diff(req.cell_y) == 1
                        });
                        let base_tx = u16::from(req.cell_x) * 4;
                        let base_ty = u16::from(req.cell_y) * 4;
                        let has_dry_land = (0u16..4).any(|dx| {
                            (0u16..4).any(|dy| terrain.is_dry_land(base_tx + dx, base_ty + dy))
                        });
                        if !is_adjacent || !has_dry_land {
                            let abort = AbortMsg {
                                text: "Expanded domain cell must be adjacent dry land.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                        rec.cells.push(cell);
                        if state.db.upsert_estate(&rec).is_err() {
                            let abort = AbortMsg {
                                text: "Domain cell is already claimed by another survivor.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    state.db.log_audit(
                            now,
                            "DOMAIN",
                            &authed_name,
                            &peer_ip,
                            &rec.region_id,
                            &format!("Expanded domain cell ({}, {})", req.cell_x, req.cell_y),
                        );
                    }
                    let reply = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ESTATE_LICENSE,
                        &estate_record_to_msg(&rec).pack()?,
                    )?;
                    let _ = tx.send(reply).await;
                    let chunk_coord = (req.cell_x / 4, req.cell_y / 4);
                    if let Some(grids) =
                        build_estate_grids_for_region(&state, &rec.region_id, vec![chunk_coord])
                    {
                    let gf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_GRIDS,
                        &grids.pack()?,
                    )?;
                    state.broadcast_region_all(&rec.region_id, &gf).await;
                    }
                    let el = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_LICENSES,
                        &build_estate_licenses_msg(&state, &eid).pack()?,
                    )?;
                    let _ = tx.send(el).await;
                }
                TC_SHRINK_ESTATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EstateCellOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let Some(mut rec) = state.db.get_estate_by_id(&req.estate_id).ok().flatten()
                    else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    let _ = state.db.purge_expired_estates(&rec.region_id, now);
                    if rec.expires_at <= now || !is_estate_owner(&state, &eid, &rec) {
                        let abort = AbortMsg {
                            text: "Cannot shrink this domain.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let target_cell = (req.cell_x, req.cell_y);
                    if !rec.cells.contains(&target_cell) || rec.cells.len() <= 1 {
                        let abort = AbortMsg {
                            text: "Cannot shrink the last domain cell.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let remaining: Vec<(u8, u8)> = rec
                        .cells
                        .iter()
                        .copied()
                        .filter(|&c| c != target_cell)
                        .collect();
                    let rem_set: HashSet<(u8, u8)> = remaining.iter().copied().collect();
                    let mut visited = HashSet::new();
                    let mut q = VecDeque::new();
                    if let Some(&start) = remaining.first() {
                        visited.insert(start);
                        q.push_back(start);
                    }
                    while let Some((cx, cy)) = q.pop_front() {
                        for (nx, ny) in [
                            (cx.wrapping_sub(1), cy),
                            (cx.saturating_add(1), cy),
                            (cx, cy.wrapping_sub(1)),
                            (cx, cy.saturating_add(1)),
                        ] {
                            let nb = (nx, ny);
                            if (nx.abs_diff(cx) + ny.abs_diff(cy) == 1)
                                && rem_set.contains(&nb)
                                && visited.insert(nb)
                            {
                                q.push_back(nb);
                            }
                        }
                    }
                    if visited.len() != rem_set.len() {
                        let abort = AbortMsg {
                            text: "Cannot split a domain into disconnected parts.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    rec.cells = remaining;
                    if let Some(&(first_cx, first_cy)) = rec.cells.first() {
                        if rec.anchor_x / 4 == u16::from(req.cell_x)
                            && rec.anchor_y / 4 == u16::from(req.cell_y)
                        {
                            rec.anchor_x = u16::from(first_cx) * 4 + 2;
                            rec.anchor_y = u16::from(first_cy) * 4 + 2;
                        }
                    }
                    let _ = state.db.upsert_estate(&rec);
                    state.db.log_audit(
                        now,
                        "DOMAIN",
                        &authed_name,
                        &peer_ip,
                        &rec.region_id,
                        &format!("Shrunk domain cell ({}, {})", req.cell_x, req.cell_y),
                    );
                    let reply = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ESTATE_LICENSE,
                        &estate_record_to_msg(&rec).pack()?,
                    )?;
                    let _ = tx.send(reply).await;
                    let chunk_coord = (req.cell_x / 4, req.cell_y / 4);
                    let grids = build_estate_grids_for_region(&state, &rec.region_id, vec![chunk_coord])
                        .unwrap_or(EstateGridsMsg {
                            chunks: vec![chunk_coord],
                            cells: Vec::new(),
                            estate_licenses: Vec::new(),
                        });
                    let gf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_GRIDS,
                        &grids.pack()?,
                    )?;
                    state.broadcast_region_all(&rec.region_id, &gf).await;
                    let el = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_LICENSES,
                        &build_estate_licenses_msg(&state, &eid).pack()?,
                    )?;
                    let _ = tx.send(el).await;
                }
                TC_EXTEND_ESTATE_ACTIVATION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = ExtendEstateActivationMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let Some(mut rec) = state.db.get_estate_by_id(&req.estate_id).ok().flatten()
                    else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    let _ = state.db.purge_expired_estates(&rec.region_id, now);
                    if rec.expires_at <= now || !is_estate_owner(&state, &eid, &rec) {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    }
                    rec.expires_at = (rec.expires_at.max(now) + ESTATE_LEASE_DAYS * 86_400.0)
                        .min(now + ESTATE_LEASE_DAYS * 86_400.0);
                    let _ = state.db.upsert_estate(&rec);
                    state.db.log_audit(
                        now,
                        "DOMAIN",
                        &authed_name,
                        &peer_ip,
                        &rec.region_id,
                        &format!("Extended domain lease {}", rec.estate_id),
                    );
                    let reply = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ESTATE_LICENSE,
                        &estate_record_to_msg(&rec).pack()?,
                    )?;
                    let _ = tx.send(reply).await;
                    let el = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ESTATE_LICENSES,
                        &build_estate_licenses_msg(&state, &eid).pack()?,
                    )?;
                    let _ = tx.send(el).await;
                }
                TC_REMOVE_ESTATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if let Some(rec) = state.db.get_estate_by_id(&req.value).ok().flatten() {
                        if is_estate_owner(&state, &eid, &rec) {
                            let chunks: Vec<(u8, u8)> = {
                                let mut set = HashSet::new();
                                for &(cx, cy) in &rec.cells {
                                    set.insert((cx / 4, cy / 4));
                                }
                                set.into_iter().collect()
                            };
                            let _ = state.db.delete_estate(&rec.estate_id);
                    state.db.log_audit(
                                now,
                                "DOMAIN",
                                &authed_name,
                                &peer_ip,
                                &rec.region_id,
                                &format!("Relinquished domain {}", rec.estate_id),
                            );
                            let ok = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_OK,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(ok).await;
                            let grids = build_estate_grids_for_region(&state, &rec.region_id, chunks.clone())
                                .unwrap_or(EstateGridsMsg {
                                    chunks,
                                    cells: Vec::new(),
                                    estate_licenses: Vec::new(),
                                });
                            let gf = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ESTATE_GRIDS,
                                &grids.pack()?,
                            )?;
                            state.broadcast_region_all(&rec.region_id, &gf).await;
                            let el = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ESTATE_LICENSES,
                                &build_estate_licenses_msg(&state, &eid).pack()?,
                            )?;
                            let _ = tx.send(el).await;
                            continue;
                        }
                    }
                    let fail = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_FAILED,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(fail).await;
                }
                TC_RETURN_TO_ESTATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = ReturnToEstateMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot return to domain while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let _ = state.db.purge_expired_estates(&region_id, now);
                    let mut estates = state.db.list_estates_by_owner(&eid).unwrap_or_default();
                    if let Ok(Some((clan_id, _, _))) = state.db.get_player_clan(&eid) {
                        estates.extend(state.db.list_estates_by_owner(&clan_id).unwrap_or_default());
                    }
                    estates.retain(|e| e.expires_at > now);
                    let target = estates
                        .iter()
                        .find(|e| e.owner_type == req.owner_type)
                        .or_else(|| estates.first());
                    let Some(target) = target else {
                        let abort = AbortMsg {
                            text: "No active domain found.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    let target_region = target.region_id.clone();
                    let dest_tile = (target.anchor_x, target.anchor_y);
                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    active_craft_gen.fetch_add(1, Ordering::Relaxed);
                    if let Some(cancel_tx) = active_craft_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    if target_region != region_id {
                        let target_terrain = crate::state::terrain_id_for_region(&target_region);
                        emigrated = true;
                        let wx = f32::from(dest_tile.0) * 200.0;
                        let wy = f32::from(dest_tile.1) * 200.0;
                        let _ = state
                            .db
                            .update_character_region(&eid, &target_region, &target_terrain);
                        let _ = state.db.update_position(&eid, wx, wy, now);
                        if let Some(tmp) = state.temp_characters.write().await.get_mut(&eid) {
                            tmp.region_id = target_region.clone();
                            tmp.terrain_id = target_terrain.clone();
                            tmp.pos_x = wx;
                            tmp.pos_y = wy;
                        }
                        if let Some(client) = state.live_clients.write().await.get_mut(&eid) {
                            client.stop_rest(now);
                            client.region_id = target_region.clone();
                            client.pos_x = wx;
                            client.pos_y = wy;
                            client.in_battle = false;
                            client.battle_target_id = None;
                            client.boarding_on = BOARDING_ON_NONE;
                            client.boarding_since = None;
                            client.boarding_until = None;
                            client.vehicle_entity_id.clear();
                            client.warp_generation = client.warp_generation.wrapping_add(1);
                        }
                    state.db.log_audit(
                            now,
                            "DOMAIN",
                            &authed_name,
                            &peer_ip,
                            &target_region,
                            &format!("Returned across islands to domain ({}, {})", dest_tile.0, dest_tile.1),
                        );
                        let ok = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_OK,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(ok).await;
                        let em = EmigratedMsg {
                            teleport_type: TELEPORT_TYPE_RETURNING,
                        };
                        let em_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_EMIGRATED,
                            &em.pack()?,
                        )?;
                        let _ = tx.send(em_frame).await;
                        continue;
                    }
                    let warp_dur = if state.config.collect_delay_ms < 200 {
                        0.05
                    } else {
                        1.5
                    };
                    state.db.log_audit(
                        ServerState::now_unix_secs(),
                        "DOMAIN",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Returned to domain ({}, {})", dest_tile.0, dest_tile.1),
                    );
                    start_warp_sequence(
                        state.clone(),
                        tx.clone(),
                        next_seq.clone(),
                        eid,
                        conn_id,
                        region_id,
                        h.seq,
                        dest_tile,
                        TELEPORT_TYPE_RETURNING,
                        warp_dur,
                    )
                    .await?;
                }
                TC_SET_ESTATE_LICENSE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SetEstateLicenseMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if let Some(mut rec) = state.db.get_estate_by_id(&req.estate_id).ok().flatten() {
                        let _ = state.db.purge_expired_estates(&rec.region_id, now);
                        if rec.expires_at > now && is_estate_owner(&state, &eid, &rec) {
                            rec.access_for_others = req.access_for_others;
                            let _ = state.db.upsert_estate(&rec);
                    state.db.log_audit(
                                now,
                                "DOMAIN",
                                &authed_name,
                                &peer_ip,
                                &rec.region_id,
                                &format!("Updated domain access={}", req.access_for_others),
                            );
                            let ok = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_OK,
                                &pack_empty_struct()?,
                            )?;
                            let _ = tx.send(ok).await;
                            let chunks: Vec<(u8, u8)> = {
                                let mut set = HashSet::new();
                                for &(cx, cy) in &rec.cells {
                                    set.insert((cx / 4, cy / 4));
                                }
                                set.into_iter().collect()
                            };
                            if let Some(grids) =
                                build_estate_grids_for_region(&state, &rec.region_id, chunks)
                            {
                                let gf = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_ESTATE_GRIDS,
                                    &grids.pack()?,
                                )?;
                                state.broadcast_region_all(&rec.region_id, &gf).await;
                            }
                            let el = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ESTATE_LICENSES,
                                &build_estate_licenses_msg(&state, &eid).pack()?,
                            )?;
                            let _ = tx.send(el).await;
                            continue;
                        }
                    }
                    let fail = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_FAILED,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(fail).await;
                }
                TC_SET_PERSONAL_REGION_ADMISSION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SetPersonalRegionAdmissionMsg::unpack(&frame.msgpack)?;
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let mut pts = ensure_personal_region_points(&state, &eid, region_id);
                    pts.admission_categories = req.admission_categories;
                    let _ = state.db.upsert_player_points(&eid, &pts);
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_KICK_VISITOR | TC_VISIT_ESTATE | TC_SET_ARTIFACT_ACCESS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let abort = AbortMsg {
                        text: "This domain action is not yet supported.".into(),
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ABORT,
                        &abort.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_WARP_TO_PORT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let (cur_x, cur_y) = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .map(|c| (c.pos_x, c.pos_y))
                        .unwrap_or((
                            f32::from(terrain.entry_tile.0) * 200.0,
                            f32::from(terrain.entry_tile.1) * 200.0,
                        ));
                    let dest_tile = nearest_point(&terrain.pois.port_points, cur_x, cur_y)
                        .unwrap_or(terrain.entry_tile);

                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    {
                        let mut animals = state.animals.write().await;
                        for a in animals.values_mut() {
                            if a.enemy_id.as_deref() == Some(eid.as_str()) {
                                a.enemy_id = None;
                            }
                        }
                    }

                    let warp_dur = if state.config.collect_delay_ms < 200 {
                        0.05
                    } else {
                        1.5
                    };
                    tracing::info!(%peer, entity = %eid, ?dest_tile, "TCP WarpToPort -> Timer + Teleported");
                    state.db.log_audit(
                        ServerState::now_unix_secs(),
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Warped to Harbor ({}, {})", dest_tile.0, dest_tile.1),
                    );
                    start_warp_sequence(
                        state.clone(),
                        tx.clone(),
                        next_seq.clone(),
                        eid,
                        conn_id,
                        region_id,
                        h.seq,
                        dest_tile,
                        TELEPORT_TYPE_RETURNING,
                        warp_dur,
                    )
                    .await?;
                }
                TC_RETURN_TO_HOME => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot return home while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let pts_msg = build_points_msg(&state, &eid, &region_id).await;
                    let (dest_region, dest_tile) = if let Some(hp) = pts_msg.home_point.as_ref() {
                        (hp.region.id.clone(), (hp.tile_x, hp.tile_y))
                    } else {
                        (
                            pts_msg.returning_point.region.id.clone(),
                            (pts_msg.returning_point.tile_x, pts_msg.returning_point.tile_y),
                        )
                    };

                    active_gather_gen.fetch_add(1, Ordering::Relaxed);
                    active_gather_target = None;
                    if let Some(cancel_tx) = active_gather_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    active_craft_gen.fetch_add(1, Ordering::Relaxed);
                    if let Some(cancel_tx) = active_craft_cancel.take() {
                        let _ = cancel_tx.send(());
                    }
                    if dest_region != region_id {
                        let target_terrain = crate::state::terrain_id_for_region(&dest_region);
                        emigrated = true;
                        let wx = f32::from(dest_tile.0) * 200.0;
                        let wy = f32::from(dest_tile.1) * 200.0;
                        let _ = state
                            .db
                            .update_character_region(&eid, &dest_region, &target_terrain);
                        let _ = state.db.update_position(&eid, wx, wy, now);
                        if let Some(tmp) = state.temp_characters.write().await.get_mut(&eid) {
                            tmp.region_id = dest_region.clone();
                            tmp.terrain_id = target_terrain.clone();
                            tmp.pos_x = wx;
                            tmp.pos_y = wy;
                        }
                        if let Some(client) = state.live_clients.write().await.get_mut(&eid) {
                            client.stop_rest(now);
                            client.region_id = dest_region.clone();
                            client.pos_x = wx;
                            client.pos_y = wy;
                            client.in_battle = false;
                            client.battle_target_id = None;
                            client.boarding_on = BOARDING_ON_NONE;
                            client.boarding_since = None;
                            client.boarding_until = None;
                            client.vehicle_entity_id.clear();
                            client.warp_generation = client.warp_generation.wrapping_add(1);
                        }
                    state.db.log_audit(
                            now,
                            "WORLD",
                            &authed_name,
                            &peer_ip,
                            &dest_region,
                            &format!("Returned across islands to Home ({}, {})", dest_tile.0, dest_tile.1),
                        );
                        let ok = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_OK,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(ok).await;
                        let em = EmigratedMsg {
                            teleport_type: TELEPORT_TYPE_RETURNING,
                        };
                        let em_frame = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_EMIGRATED,
                            &em.pack()?,
                        )?;
                        let _ = tx.send(em_frame).await;
                        continue;
                    }
                    let warp_dur = if state.config.collect_delay_ms < 200 {
                        0.05
                    } else {
                        1.5
                    };
                    tracing::info!(%peer, entity = %eid, ?dest_tile, "TCP ReturnToHome -> Timer + Teleported");
                    state.db.log_audit(
                        ServerState::now_unix_secs(),
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Returned to Home ({}, {})", dest_tile.0, dest_tile.1),
                    );
                    start_warp_sequence(
                        state.clone(),
                        tx.clone(),
                        next_seq.clone(),
                        eid,
                        conn_id,
                        region_id,
                        h.seq,
                        dest_tile,
                        TELEPORT_TYPE_RETURNING,
                        warp_dur,
                    )
                    .await?;
                }
                TC_SET_AS_HOME => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let Some(art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let abort = AbortMsg {
                            text: "Structure not found.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    if art.building_state != BUILDING_STATE_COMPLETED {
                        let abort = AbortMsg {
                            text: "Structure must be completed first.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, region_id, &art, false)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let is_home = state
                        .catalog
                        .resolve_artifact_type(art.entity_type)
                        .map(|s| s.is_home || s.can_rest)
                        .unwrap_or(true);
                    if !is_home {
                        let abort = AbortMsg {
                            text: "Only beds or tents can be set as your home.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let mut pts = ensure_personal_region_points(&state, &eid, region_id);
                    pts.home_artifact_id = Some(art.entity_id);
                    let _ = state.db.upsert_player_points(&eid, &pts);
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        region_id,
                        &format!(
                            "Set '{}' at ({}, {}) as Home",
                            art.blueprint_id, art.tile_x, art.tile_y
                        ),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let pmsg = build_points_msg(&state, &eid, region_id).await;
                    let pf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_POINTS,
                        &pmsg.pack()?,
                    )?;
                    let _ = tx.send(pf).await;
                }
                TC_SET_RETURNING_POINT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot set returning point while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    if usize::from(req.tile_x) >= terrain.width
                        || usize::from(req.tile_y) >= terrain.height
                        || !terrain.is_dry_land(req.tile_x, req.tile_y)
                    {
                        let abort = AbortMsg {
                            text: "Returning point must be on dry land inside the island.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let cur_pos = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .map(|c| (c.pos_x / 200.0, c.pos_y / 200.0));
                    if let Some((px, py)) = cur_pos {
                        if (px - f32::from(req.tile_x)).hypot(py - f32::from(req.tile_y)) > 16.0 {
                            let abort = AbortMsg {
                                text: "Move closer to set your returning point here.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    }
                    let mut pts = ensure_personal_region_points(&state, &eid, region_id);
                    pts.returning_x = Some(req.tile_x);
                    pts.returning_y = Some(req.tile_y);
                    pts.returning_region_id = Some(region_id.to_string());
                    let _ = state.db.upsert_player_points(&eid, &pts);
                    state.db.log_audit(
                        now,
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        region_id,
                        &format!("Updated returning point to ({}, {})", req.tile_x, req.tile_y),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let pmsg = build_points_msg(&state, &eid, region_id).await;
                    let pf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_POINTS,
                        &pmsg.pack()?,
                    )?;
                    let _ = tx.send(pf).await;
                }
                TC_REMOVE_DEATH_POINT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let now = ServerState::now_unix_secs();
                    let pmsg = build_points_msg(&state, &eid, region_id).await;
                    let pf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_POINTS,
                        &pmsg.pack()?,
                    )?;
                    let _ = tx.send(pf).await;
                }
                TC_GET_DEFOGGED_CHUNKS => {
                    if !is_ready {
                        continue;
                    }
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    let mut all_chunks =
                        Vec::with_capacity(terrain.num_chunks_x() * terrain.num_chunks_y());
                    for cx in 0..terrain.num_chunks_x() {
                        for cy in 0..terrain.num_chunks_y() {
                            all_chunks.push((cx as u8, cy as u8));
                        }
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_DEFOGGED_CHUNKS,
                        &DefoggedChunks { chunks: all_chunks }.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_POI_COUNT => {
                    if !is_ready {
                        continue;
                    }
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let region_id = if req.value.is_empty() {
                        authed_region
                            .as_deref()
                            .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                            .to_string()
                    } else {
                        req.value
                    };
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let now = ServerState::now_unix_secs();
                    let msg = PoiCountMsg {
                        port_count: terrain.pois.port_points.len().min(u8::MAX as usize) as u8,
                        warphole_count: terrain.pois.warpholes.len().min(u8::MAX as usize) as u8,
                        crater_count: terrain.pois.craters.len().min(u8::MAX as usize) as u8,
                        rift_count: terrain.pois.rifts.len().min(u8::MAX as usize) as u8,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_POI_COUNT,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_EXPLORED_POIS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let region_id = if req.value.is_empty() {
                        authed_region
                            .as_deref()
                            .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                            .to_string()
                    } else {
                        req.value
                    };
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let saved = state
                        .db
                        .list_explored_pois(&eid, &region_id)
                        .unwrap_or_default();
                    let mut by_tile: BTreeMap<(u16, u16), PointOfInterestMsg> = BTreeMap::new();
                    // Always expose Harbor ports from terrain pois.yml so Harbor icon shows on map
                    for &(x, y) in &terrain.pois.port_points {
                        by_tile.insert(
                            (x, y),
                            PointOfInterestMsg {
                                tile_x: x,
                                tile_y: y,
                                poi_type: POI_PORT,
                                icon: None,
                                title: None,
                                entity_type: Some(7001),
                                is_explored: true,
                            },
                        );
                    }
                    for &(x, y) in &terrain.pois.warpholes {
                        by_tile.insert(
                            (x, y),
                            PointOfInterestMsg {
                                tile_x: x,
                                tile_y: y,
                                poi_type: POI_WARPHOLE,
                                icon: None,
                                title: None,
                                entity_type: Some(9450),
                                is_explored: true,
                            },
                        );
                    }
                    for p in saved {
                        by_tile.insert(
                            (p.tile_x, p.tile_y),
                            PointOfInterestMsg {
                                tile_x: p.tile_x,
                                tile_y: p.tile_y,
                                poi_type: p.poi_type,
                                icon: if p.icon.is_empty() { None } else { Some(p.icon) },
                                title: if p.title.is_empty() { None } else { Some(p.title) },
                                entity_type: p.entity_type,
                                is_explored: true,
                            },
                        );
                    }
                    let now = ServerState::now_unix_secs();
                    let msg = ExploredPoisMsg {
                        pois: by_tile.into_values().collect(),
                        full_count_rewarded: false,
                        is_opened_map: true,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_EXPLORED_POIS,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_EXPLORE_POI => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = PointOfInterestMsg::unpack(&frame.msgpack)?;
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    if usize::from(req.tile_x) >= terrain.width
                        || usize::from(req.tile_y) >= terrain.height
                    {
                        continue;
                    }
                    let icon: String = req.icon.unwrap_or_default().chars().take(64).collect();
                    let title: String = req.title.unwrap_or_default().chars().take(64).collect();
                    let existing = state
                        .db
                        .list_explored_pois(&eid, &region_id)
                        .unwrap_or_default();
                    let already = existing.iter().any(|p| {
                        p.tile_x == req.tile_x && p.tile_y == req.tile_y && p.poi_type == req.poi_type
                    });
                    if existing.len() >= 64 && !already {
                        continue;
                    }
                    let _ = state.db.upsert_explored_poi(
                        &eid,
                        &ExploredPoiRecord {
                            region_id: region_id.clone(),
                            tile_x: req.tile_x,
                            tile_y: req.tile_y,
                            poi_type: req.poi_type,
                            icon,
                            title,
                            entity_type: req.entity_type,
                        },
                    );
                    let now = ServerState::now_unix_secs();
                    state.db.log_audit(
                        now,
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!(
                            "Discovered POI (type={}) at ({}, {})",
                            req.poi_type, req.tile_x, req.tile_y
                        ),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_LAST_SEARCHED_TIME => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_LAST_SEARCHED_TIME,
                        &LastSearchedTimeMsg {
                            searched_at: last_poi_searched_at,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_SEARCH_POIS => {
                    if !is_ready {
                        continue;
                    }
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    let terrain = state.terrains.get_or_load(&region_id).await;
                    let now = ServerState::now_unix_secs();
                    last_poi_searched_at = now;
                    let mut results = Vec::new();
                    for &(x, y) in &terrain.pois.port_points {
                        results.push((x, y, POI_PORT));
                    }
                    for &(x, y) in &terrain.pois.warpholes {
                        results.push((x, y, POI_WARPHOLE));
                    }
                    for &(x, y) in &terrain.pois.rifts {
                        results.push((x, y, POI_RIFT));
                    }
                    for &(x, y) in &terrain.pois.craters {
                        results.push((x, y, POI_CRATER));
                    }
                    state.db.log_audit(
                        now,
                        "WORLD",
                        &authed_name,
                        &peer_ip,
                        &region_id,
                        &format!("Scanned island POIs ({} found)", results.len()),
                    );
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_SEARCHED_POIS,
                        &SearchedPoisMsg {
                            results,
                            searched_at: now,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_REQUEST_NEAREST_POI => {
                    if !is_ready {
                        continue;
                    }
                    let req = RequestNearestPoiMsg::unpack(&frame.msgpack)?;
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let terrain = state.terrains.get_or_load(region_id).await;
                    let candidates: &[(u16, u16)] = match req.poi_type {
                        POI_PORT => &terrain.pois.port_points,
                        POI_WARPHOLE => &terrain.pois.warpholes,
                        POI_CRATER => &terrain.pois.craters,
                        POI_RIFT => &terrain.pois.rifts,
                        _ => &terrain.pois.port_points,
                    };
                    let tile = nearest_point(
                        candidates,
                        f32::from(req.tile_x) * 200.0,
                        f32::from(req.tile_y) * 200.0,
                    );
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_NEAREST_POI,
                        &NearestPoiMsg {
                            poi_type: req.poi_type,
                            tile,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_REGION => {
                    if !is_ready {
                        continue;
                    }
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let region_id = if req.value.is_empty() {
                        authed_region
                            .as_deref()
                            .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                            .to_string()
                    } else {
                        req.value
                    };
                    let region = build_region_msg(&state, &region_id).await;
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_REGION,
                        &region.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ARCHIPELAGO => {
                    if !is_ready {
                        continue;
                    }
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let msg = build_archipelago_msg(&req.value, now);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ARCHIPELAGO,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ROUTES => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let msg = build_routes_msg(&state);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ROUTES,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_DUMP_ITEMS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = DumpItemsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let items = state.db.list_items(&eid).unwrap_or_default();
                    let eq_ids: HashSet<String> = state
                        .db
                        .list_equipped(&eid)
                        .unwrap_or_default()
                        .into_iter()
                        .map(|(_, id)| id)
                        .collect();
                    let mut seen_drop = HashSet::new();
                    let mut to_remove = Vec::new();
                    for id in &req.item_ids {
                        if !seen_drop.insert(id.clone()) {
                            continue;
                        }
                        let can_drop = items
                            .iter()
                            .find(|it| &it.item_id == id)
                            .is_some_and(|it| !it.locked && !eq_ids.contains(&it.item_id));
                        if can_drop {
                            to_remove.push(id.clone());
                        }
                    }
                    if !to_remove.is_empty() {
                        let dropped_names: Vec<String> = items
                            .iter()
                            .filter(|it| to_remove.contains(&it.item_id))
                            .map(|it| format!("{} (Lv.{})", it.name, it.level))
                            .collect();
                        let _ = state.db.delete_items(&eid, &to_remove);
                    state.db.log_audit(
                            now,
                            "ITEM",
                            &authed_name,
                            &peer_ip,
                            authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                            &format!("Dropped {}", dropped_names.join(", ")),
                        );
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid,
                            items: Vec::new(),
                            removed_item_ids: to_remove,
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_INVENTORY => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = GetInventoryMsg::unpack(&frame.msgpack).unwrap_or(GetInventoryMsg {
                        target_entity_id: None,
                    });
                    let inv = match req.target_entity_id {
                        Some(ref cid) if cid != &eid => {
                            if let Err(reason) = validate_storage_artifact(
                                &state,
                                &eid,
                                &authed_owner,
                                cur_region,
                                cid,
                            )
                            .await
                            {
                                let abort = AbortMsg {
                                    text: reason.into(),
                                };
                                let bytes = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    h.seq,
                                    TC_ABORT,
                                    &abort.pack()?,
                                )?;
                                let _ = tx.send(bytes).await;
                                continue;
                            }
                            build_container_inventory_msg(&state, cid, None, now)
                        }
                        _ => build_inventory_msgs(&state, &eid, now).0,
                    };
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_INVENTORY,
                        &inv.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_CHANGE_EQUIP_SLOT_TYPE => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_OPEN_GATE | TC_CLOSE_GATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let opened = h.type_code == TC_OPEN_GATE;
                    if let Ok(Some(mut art)) = state.db.get_artifact(&req.entity_id) {
                        if let Err(reason) =
                            can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                                .await
                        {
                            let abort = AbortMsg {
                                text: reason.into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                        art.gate_opened = opened;
                        let _ = state.db.update_artifact_flags(
                            &art.entity_id,
                            art.gate_opened,
                            art.is_lit,
                            &art.materials_json,
                        );
                        let appear = artifact_record_to_appear_msg(&art, &state.catalog, now);
                        let sf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            durango_wire::TC_ARTIFACT_STATE,
                            &appear.state_msg_with_gate(opened).pack()?,
                        )?;
                        state.broadcast_region_all(&art.region_id, &sf).await;
                    }
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        if opened { "Opened gate" } else { "Closed gate" },
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_REPAIR_ITEM => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = RepairItemMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if req.kit_item_ids.is_empty() {
                        let abort = AbortMsg {
                            text: "Select at least one repair kit to repair this item.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let items = state.db.list_items(&eid).unwrap_or_default();
                    let eq_ids = state.db.equipped_item_ids(&eid).unwrap_or_default();
                    let Some(mut target) = items.iter().find(|it| it.item_id == req.item_id).cloned()
                    else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    let mut seen_kits = HashSet::new();
                    let removed: Vec<String> = req
                        .kit_item_ids
                        .iter()
                        .filter(|kid| {
                            *kid != &target.item_id
                                && seen_kits.insert((*kid).clone())
                                && items.iter().any(|it| {
                                    &it.item_id == *kid
                                        && !it.locked
                                        && !eq_ids.contains(&it.item_id)
                                })
                        })
                        .cloned()
                        .collect();
                    if removed.is_empty() || state.db.delete_items(&eid, &removed).is_err() {
                        let abort = AbortMsg {
                            text: "No valid unlocked repair kit available.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    target.durability = target.max_durability.max(100.0);
                    let _ = state
                        .db
                        .update_item_durability(&eid, &target.item_id, target.durability);
                    state.db.log_audit(
                        now,
                        "ITEM",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!("Repaired item {} (Lv.{})", target.name, target.level),
                    );
                    let upd = InventoryUpdatedMsg {
                        entity_id: eid,
                        items: vec![item_record_to_msg(&target, &state.catalog, now)],
                        removed_item_ids: removed,
                        item_order: None,
                    };
                    let uf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY_UPDATED,
                        &upd.pack()?,
                    )?;
                    let _ = tx.send(uf).await;
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_REPAIR_ARTIFACT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = RepairArtifactMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let Some(mut art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if req.kit_item_ids.is_empty() {
                        let abort = AbortMsg {
                            text: "Select at least one repair kit to repair this structure.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let items = state.db.list_items(&eid).unwrap_or_default();
                    let eq_ids = state.db.equipped_item_ids(&eid).unwrap_or_default();
                    let mut seen_kits = HashSet::new();
                    let removed: Vec<String> = req
                        .kit_item_ids
                        .iter()
                        .filter(|kid| {
                            seen_kits.insert((*kid).clone())
                                && items.iter().any(|it| {
                                    &it.item_id == *kid
                                        && !it.locked
                                        && !eq_ids.contains(&it.item_id)
                                })
                        })
                        .cloned()
                        .collect();
                    if removed.is_empty() || state.db.delete_items(&eid, &removed).is_err() {
                        let abort = AbortMsg {
                            text: "No valid unlocked repair kit available.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let upd = InventoryUpdatedMsg {
                        entity_id: eid,
                        items: Vec::new(),
                        removed_item_ids: removed,
                        item_order: None,
                    };
                    let uf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_INVENTORY_UPDATED,
                        &upd.pack()?,
                    )?;
                    let _ = tx.send(uf).await;

                    art.durability = art.max_durability.max(100.0);
                    art.last_decay_at = now;
                    let _ = state
                        .db
                        .update_artifact_durability(&art.entity_id, art.durability, now);
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        &art.region_id,
                        &format!(
                            "Repaired structure '{}' at ({}, {})",
                            art.blueprint_id, art.tile_x, art.tile_y
                        ),
                    );
                    let appear = artifact_record_to_appear_msg(&art, &state.catalog, now);
                    let sf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        durango_wire::TC_ARTIFACT_STATE,
                        &appear.state_msg().pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &sf).await;
                    let df = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        durango_wire::TC_ARTIFACT_DISPLAY,
                        &appear.display_msg().pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &df).await;
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_REPAIR_IMMEDIATE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = RepairImmediateMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let Some(mut art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let fail = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_FAILED,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(fail).await;
                        continue;
                    };
                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let missing_dur = (art.max_durability.max(100.0) - art.durability).max(0.0);
                    let cost = if req.cost_amount > 0 {
                        i64::from(req.cost_amount)
                    } else if missing_dur > 0.01 {
                        (missing_dur.ceil() as i64 * 10).max(10)
                    } else {
                        0
                    };
                    if cost > 0 {
                        match state.db.try_spend_t_stone(&eid, cost) {
                            Ok(Some(new_bal)) => {
                                let wf = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    0,
                                    TC_WALLET_UPDATED,
                                    &WalletUpdatedMsg {
                                        entity_id: eid.clone(),
                                        t_stone: new_bal,
                                    }
                                    .pack()?,
                                )?;
                                let _ = tx.send(wf).await;
                            }
                            Ok(None) => {
                                let abort = AbortMsg {
                                    text: "Not enough T-Stone for immediate repair.".into(),
                                };
                                let bytes = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    h.seq,
                                    TC_ABORT,
                                    &abort.pack()?,
                                )?;
                                let _ = tx.send(bytes).await;
                                continue;
                            }
                            Err(_) => {
                                let fail = encode_frame(
                                    now,
                                    next_seq.fetch_add(1, Ordering::Relaxed),
                                    h.seq,
                                    TC_FAILED,
                                    &pack_empty_struct()?,
                                )?;
                                let _ = tx.send(fail).await;
                                continue;
                            }
                        }
                    }
                    art.durability = art.max_durability.max(100.0);
                    art.last_decay_at = now;
                    let _ = state
                        .db
                        .update_artifact_durability(&art.entity_id, art.durability, now);
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        &art.region_id,
                        &format!(
                            "Immediately repaired '{}' at ({}, {}) for {} T-Stone",
                            art.blueprint_id, art.tile_x, art.tile_y, cost
                        ),
                    );
                    let appear = artifact_record_to_appear_msg(&art, &state.catalog, now);
                    let sf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        durango_wire::TC_ARTIFACT_STATE,
                        &appear.state_msg().pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &sf).await;
                    let df = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        durango_wire::TC_ARTIFACT_DISPLAY,
                        &appear.display_msg().pack()?,
                    )?;
                    state.broadcast_region_all(&art.region_id, &df).await;
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_STATUS_EFFECTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let msg = state
                        .live_clients
                        .read()
                        .await
                        .get(&eid)
                        .map(|c| build_status_effects_msg(c, now))
                        .unwrap_or_else(|| StatusEffectsMsg {
                            entity_id: eid,
                            effects: Vec::new(),
                        });
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_STATUS_EFFECTS,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_TOGGLE_STATUS_EFFECT => {
                    if !is_ready {
                        continue;
                    }
                    let _req = ToggleStatusEffectMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_DRINK_WATER | TC_WASH_BODY | TC_WASH => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let surv_res = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).and_then(|c| {
                            if !c.is_alive || c.in_battle {
                                return None;
                            }
                            if h.type_code == TC_DRINK_WATER {
                                c.energy = (c.energy + 15.0).min(100.0);
                            }
                            c.fatigue = (c.fatigue - 10.0).max(0.0);
                            Some((
                                (c.life, c.current_stamina(now), c.energy, c.fatigue),
                                c.to_survival_msg(now),
                            ))
                        })
                    };
                    let Some(((life, stam, energy, fatigue), surv)) = surv_res else {
                        let abort = AbortMsg {
                            text: "Cannot drink or wash while in battle or unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    let _ = state.db.update_survival(&eid, life, stam, energy, fatigue);
                    state.db.log_audit(
                        now,
                        "SURVIVAL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        if h.type_code == TC_DRINK_WATER {
                            "Drank water (+15 Energy, -10 Fatigue)"
                        } else {
                            "Washed (-10 Fatigue)"
                        },
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let sf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SURVIVAL,
                        &surv.pack()?,
                    )?;
                    let _ = tx.send(sf).await;
                }
                TC_CANCEL_FIRED_PROJECTILE => {
                    // Projectile cancellation — acknowledge quietly.
                }
                TC_PUT_IN_ITEM => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = ContainerItemsOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let moved = state
                        .db
                        .transfer_items_to_container(
                            &eid,
                            &req.entity_id,
                            "default",
                            &req.item_ids,
                            MAX_INVENTORY_ITEMS,
                        )
                        .unwrap_or_default();
                    let removed_ids: Vec<String> =
                        moved.iter().map(|it| it.item_id.clone()).collect();
                    if !removed_ids.is_empty() {
                    state.db.log_audit(
                            now,
                            "ITEM",
                            &authed_name,
                            &peer_ip,
                            cur_region,
                            &format!(
                                "Stored {} item(s) into container {}",
                                removed_ids.len(),
                                req.entity_id
                            ),
                        );
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid,
                            items: Vec::new(),
                            removed_item_ids: removed_ids,
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_TAKE_OUT_ITEM => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = ContainerItemsOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let taken = state
                        .db
                        .transfer_items_from_container(
                            &eid,
                            &req.entity_id,
                            None,
                            &req.item_ids,
                            MAX_INVENTORY_ITEMS,
                        )
                        .unwrap_or_default();
                    if !taken.is_empty() {
                    state.db.log_audit(
                            now,
                            "ITEM",
                            &authed_name,
                            &peer_ip,
                            cur_region,
                            &format!(
                                "Took {} item(s) from container {}",
                                taken.len(),
                                req.entity_id
                            ),
                        );
                        let msgs: Vec<ItemMsg> = taken
                            .iter()
                            .map(|it| item_record_to_msg(it, &state.catalog, now))
                            .collect();
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid,
                            items: msgs,
                            removed_item_ids: Vec::new(),
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_WAREHOUSE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let msg = build_warehouse_msg(&state, &req.entity_id);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &msg.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_SECTION_ITEMS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = WarehouseSectionOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let sec = if req.section_name.is_empty() {
                        "default"
                    } else {
                        &req.section_name
                    };
                    let inv = build_container_inventory_msg(&state, &req.entity_id, Some(sec), now);
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_INVENTORY,
                        &inv.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_ADD_ITEMS_TO_WAREHOUSE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = WarehouseItemsOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let sec = if req.section_name.is_empty() {
                        "default"
                    } else {
                        &req.section_name
                    };
                    let moved = state
                        .db
                        .transfer_items_to_container(
                            &eid,
                            &req.entity_id,
                            sec,
                            &req.item_ids,
                            MAX_INVENTORY_ITEMS,
                        )
                        .unwrap_or_default();
                    let removed_ids: Vec<String> =
                        moved.iter().map(|it| it.item_id.clone()).collect();
                    if !removed_ids.is_empty() {
                    state.db.log_audit(
                            now,
                            "ITEM",
                            &authed_name,
                            &peer_ip,
                            cur_region,
                            &format!(
                                "Stored {} item(s) in warehouse section '{}'",
                                removed_ids.len(),
                                sec
                            ),
                        );
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid,
                            items: Vec::new(),
                            removed_item_ids: removed_ids,
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    let wf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &build_warehouse_msg(&state, &req.entity_id).pack()?,
                    )?;
                    let _ = tx.send(wf).await;
                }
                TC_POP_ITEMS_FROM_WAREHOUSE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = WarehouseItemsOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let sec = if req.section_name.is_empty() {
                        None
                    } else {
                        Some(req.section_name.as_str())
                    };
                    let taken = state
                        .db
                        .transfer_items_from_container(
                            &eid,
                            &req.entity_id,
                            sec,
                            &req.item_ids,
                            MAX_INVENTORY_ITEMS,
                        )
                        .unwrap_or_default();
                    if !taken.is_empty() {
                    state.db.log_audit(
                            now,
                            "ITEM",
                            &authed_name,
                            &peer_ip,
                            cur_region,
                            &format!(
                                "Took {} item(s) from container {}",
                                taken.len(),
                                req.entity_id
                            ),
                        );
                        let msgs: Vec<ItemMsg> = taken
                            .iter()
                            .map(|it| item_record_to_msg(it, &state.catalog, now))
                            .collect();
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid,
                            items: msgs,
                            removed_item_ids: Vec::new(),
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    let wf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &build_warehouse_msg(&state, &req.entity_id).pack()?,
                    )?;
                    let _ = tx.send(wf).await;
                }
                TC_MOVE_ITEMS_IN_WAREHOUSE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = MoveItemsInWarehouseMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let _ = state.db.move_items_in_warehouse(
                        &req.entity_id,
                        &req.source_section,
                        &req.target_section,
                        &req.item_ids,
                    );
                    let wf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &build_warehouse_msg(&state, &req.entity_id).pack()?,
                    )?;
                    let _ = tx.send(wf).await;
                }
                TC_MAKE_SECTION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = WarehouseSectionOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let _ = state
                        .db
                        .upsert_warehouse_section(&req.entity_id, req.section_name.trim());
                    let wf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &build_warehouse_msg(&state, &req.entity_id).pack()?,
                    )?;
                    let _ = tx.send(wf).await;
                }
                TC_REMOVE_SECTION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = WarehouseSectionOpMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let _ = state
                        .db
                        .delete_warehouse_section(&req.entity_id, &req.section_name);
                    let wf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &build_warehouse_msg(&state, &req.entity_id).pack()?,
                    )?;
                    let _ = tx.send(wf).await;
                }
                TC_RENAME_WAREHOUSE_SECTION => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = RenameWarehouseSectionMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    if let Err(reason) = validate_storage_artifact(
                        &state,
                        &eid,
                        &authed_owner,
                        cur_region,
                        &req.entity_id,
                    )
                    .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let _ = state.db.rename_warehouse_section(
                        &req.entity_id,
                        &req.section_name,
                        req.new_name.trim(),
                    );
                    let wf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_WAREHOUSE,
                        &build_warehouse_msg(&state, &req.entity_id).pack()?,
                    )?;
                    let _ = tx.send(wf).await;
                }
                TC_SET_SECTION_ORDER | TC_SET_SECTION_ITEM_ORDER => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_REGISTER_MULTIPLE_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = RegisterMultipleProductsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE)
                        .to_string();
                    if !req.duration.is_finite()
                        || req.duration <= 0.0
                        || req.each_price <= 0
                        || req.each_price > 100_000_000
                    {
                        let abort = AbortMsg {
                            text: "Invalid market price or duration.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let batch = &req.item_ids[..req.item_ids.len().min(20)];
                    let listed = state
                        .db
                        .register_market_products(
                            &eid,
                            &region_id,
                            batch,
                            req.each_price,
                            req.duration,
                            50,
                            now,
                        )
                        .unwrap_or_default();
                    let removed: Vec<String> = listed.into_iter().map(|(iid, _)| iid).collect();
                    if !removed.is_empty() {
                    state.db.log_audit(
                            now,
                            "MARKET",
                            &authed_name,
                            &peer_ip,
                            &region_id,
                            &format!(
                                "Listed {} item(s) on market for {} T-Stone each",
                                removed.len(),
                                req.each_price.max(1)
                            ),
                        );
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid,
                            items: Vec::new(),
                            removed_item_ids: removed,
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_REGISTERED_PRODUCTS | TC_GET_EXPIRED_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let expired_only = h.type_code == TC_GET_EXPIRED_PRODUCTS;
                    let list = state
                        .db
                        .list_seller_registered_products(&eid, now, expired_only)
                        .unwrap_or_default();
                    let products: Vec<ProductMsg> = list
                        .into_iter()
                        .map(|p| {
                            let lvl = p.item.level;
                            let dur = p.item.durability;
                            let im = item_record_to_msg(&p.item, &state.catalog, now);
                            ProductMsg {
                                id: p.product_id,
                                region_id: p.region_id,
                                listed_at: p.listed_at,
                                expires_at: p.expires_at,
                                deletes_at: p.expires_at + 86_400.0,
                                price: p.price,
                                items: vec![im],
                                level: lvl,
                                durability: dur,
                            }
                        })
                        .collect();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PRODUCTS,
                        &ProductsMsg { products }.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_SOLD_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let sales = state
                        .db
                        .list_market_sales_for_seller(&eid)
                        .unwrap_or_default();
                    let products: Vec<ProductMsg> = sales
                        .into_iter()
                        .map(|s| {
                            let lvl = s.item.level;
                            let dur = s.item.durability;
                            let im = item_record_to_msg(&s.item, &state.catalog, now);
                            ProductMsg {
                                id: s.product_id,
                                region_id: s.region_id,
                                listed_at: s.sold_at,
                                expires_at: s.sold_at + 86_400.0,
                                deletes_at: s.sold_at + 172_800.0,
                                price: s.price,
                                items: vec![im],
                                level: lvl,
                                durability: dur,
                            }
                        })
                        .collect();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PRODUCTS,
                        &ProductsMsg { products }.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_PURCHASED_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let sales = state
                        .db
                        .list_market_purchases_for_buyer(&eid)
                        .unwrap_or_default();
                    let products: Vec<ProductMsg> = sales
                        .into_iter()
                        .map(|s| {
                            let lvl = s.item.level;
                            let dur = s.item.durability;
                            let im = item_record_to_msg(&s.item, &state.catalog, now);
                            ProductMsg {
                                id: s.product_id,
                                region_id: s.region_id,
                                listed_at: s.sold_at,
                                expires_at: s.sold_at + 86_400.0,
                                deletes_at: s.sold_at + 172_800.0,
                                price: s.price,
                                items: vec![im],
                                level: lvl,
                                durability: dur,
                            }
                        })
                        .collect();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PRODUCTS,
                        &ProductsMsg { products }.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_FAVORITE_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let favs = state
                        .db
                        .list_favorite_products(&eid, now)
                        .unwrap_or_default();
                    let products: Vec<ProductMsg> = favs
                        .into_iter()
                        .map(|p| {
                            let lvl = p.item.level;
                            let dur = p.item.durability;
                            let im = item_record_to_msg(&p.item, &state.catalog, now);
                            ProductMsg {
                                id: p.product_id,
                                region_id: p.region_id,
                                listed_at: p.listed_at,
                                expires_at: p.expires_at,
                                deletes_at: p.expires_at + 86_400.0,
                                price: p.price,
                                items: vec![im],
                                level: lvl,
                                durability: dur,
                            }
                        })
                        .collect();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_PRODUCTS,
                        &ProductsMsg { products }.pack()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_ADD_TO_FAVORITE_PRODUCTS | TC_REMOVE_FROM_FAVORITE_PRODUCTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SingleStringMsg::unpack(&frame.msgpack)?;
                    let fav = h.type_code == TC_ADD_TO_FAVORITE_PRODUCTS;
                    let _ = state.db.set_market_favorite(&eid, &req.value, fav);
                    let now = ServerState::now_unix_secs();
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_MARKET_COLLECT_PAYMENT | TC_MARKET_COLLECT_ALL_PAYMENTS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let pid = if h.type_code == TC_MARKET_COLLECT_PAYMENT {
                        Some(SingleStringMsg::unpack(&frame.msgpack)?.value)
                    } else {
                        None
                    };
                    let now = ServerState::now_unix_secs();
                    let (payout, new_bal) = state
                        .db
                        .collect_market_payments(&eid, pid.as_deref())
                        .unwrap_or((0, state.db.get_t_stone(&eid).unwrap_or(50_000)));
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    if payout > 0 {
                    state.db.log_audit(
                            now,
                            "MARKET",
                            &authed_name,
                            &peer_ip,
                            authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                            &format!(
                                "Collected market payout +{} T-Stone (balance: {})",
                                payout, new_bal
                            ),
                        );
                        let wf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_WALLET_UPDATED,
                            &WalletUpdatedMsg {
                                entity_id: eid,
                                t_stone: new_bal,
                            }
                            .pack()?,
                        )?;
                        let _ = tx.send(wf).await;
                    }
                }
                TC_GET_MAILS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let mf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_MAILS,
                        &build_mails_msg(&state, &eid, now).pack()?,
                    )?;
                    let _ = tx.send(mf).await;
                }
                TC_SEND_MAIL => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = SendMailMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if req.text.chars().count() > 1000 {
                        let abort = AbortMsg {
                            text: "Mail message cannot exceed 1000 characters.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if state.resolve_character(&req.recipient_id).await.is_none() {
                        let abort = AbortMsg {
                            text: "Recipient character does not exist.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let mail_id = Uuid::new_v4().to_string();
                    let removed_ids = match state.db.send_mail_atomic(
                        &eid,
                        &req.recipient_id,
                        &mail_id,
                        &req.text,
                        &req.item_ids,
                        now,
                        Some(now + MAIL_EXPIRY_DAYS * 86_400.0),
                    ) {
                        Ok(ids) => ids,
                        Err(_) => {
                            let abort = AbortMsg {
                                text: "Failed to attach one or more items.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                    };
                    let attached_cnt = removed_ids.len();
                    if !removed_ids.is_empty() {
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid.clone(),
                            items: Vec::new(),
                            removed_item_ids: removed_ids,
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    state.db.log_audit(
                        now,
                        "SOCIAL",
                        &authed_name,
                        &peer_ip,
                        authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                        &format!(
                            "Sent mail to {} ({} item(s) attached)",
                            req.recipient_id, attached_cnt
                        ),
                    );
                    // Live-push updated inbox if recipient is online
                    if let Some(recip_tx) = state
                        .live_clients
                        .read()
                        .await
                        .get(&req.recipient_id)
                        .map(|c| c.tx.clone())
                    {
                        if let Ok(packed) = build_mails_msg(&state, &req.recipient_id, now).pack() {
                            if let Ok(mf) = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_MAILS,
                                &packed,
                            ) {
                                let _ = recip_tx.try_send(mf);
                            }
                        }
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_ACCEPT_MAILS | TC_ACCEPT_USER_MAILS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = MailIdsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let is_user_mail = h.type_code == TC_ACCEPT_USER_MAILS;
                    let (items_got, ts_got, new_bal) = state
                        .db
                        .accept_mails(&eid, &req.mail_ids, is_user_mail, MAX_INVENTORY_ITEMS, now)
                        .unwrap_or_else(|_| {
                            (
                                Vec::new(),
                                0,
                                state.db.get_t_stone(&eid).unwrap_or(DEFAULT_STARTING_T_STONE),
                            )
                        });
                    if !items_got.is_empty() || ts_got > 0 {
                    state.db.log_audit(
                            now,
                            "SOCIAL",
                            &authed_name,
                            &peer_ip,
                            authed_region.as_deref().unwrap_or(crate::state::DEFAULT_REGION_CODE),
                            &format!(
                                "Claimed mail attachments ({} item(s), +{} T-Stone)",
                                items_got.len(),
                                ts_got
                            ),
                        );
                    }
                    if !items_got.is_empty() {
                        let msgs: Vec<ItemMsg> = items_got
                            .iter()
                            .map(|it| item_record_to_msg(it, &state.catalog, now))
                            .collect();
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid.clone(),
                            items: msgs,
                            removed_item_ids: Vec::new(),
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    if ts_got > 0 {
                        let wf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_WALLET_UPDATED,
                            &WalletUpdatedMsg {
                                entity_id: eid.clone(),
                                t_stone: new_bal,
                            }
                            .pack()?,
                        )?;
                        let _ = tx.send(wf).await;
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let mf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_MAILS,
                        &build_mails_msg(&state, &eid, now).pack()?,
                    )?;
                    let _ = tx.send(mf).await;
                }
                TC_DELETE_MAILS | TC_DELETE_USER_MAILS => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = MailIdsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let is_user_mail = h.type_code == TC_DELETE_USER_MAILS;
                    let _ = state.db.delete_mails(&eid, &req.mail_ids, is_user_mail);
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let mf = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_MAILS,
                        &build_mails_msg(&state, &eid, now).pack()?,
                    )?;
                    let _ = tx.send(mf).await;
                }
                TC_MARK_MAILS_AS_READ | TC_MARK_USER_MAILS_AS_READ => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let req = MailIdsMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let _ = state.db.mark_mails_read(&eid, &req.mail_ids);
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_TUTORIAL_EVENT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let ev = SingleStringMsg::unpack(&frame.msgpack)
                        .map(|m| m.value)
                        .unwrap_or_default();
                    let now = ServerState::now_unix_secs();
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let is_vitals_ev = matches!(
                        ev.as_str(),
                        "init_health" | "resurrect" | "restore_food_k" | "set_tired"
                    );
                    if !is_vitals_ev {
                        continue;
                    }
                    let char_lv = state
                        .resolve_character(&eid)
                        .await
                        .map_or(1, |c| c.level.max(1));
                    let tut_key = format!("_tut_ev_{}", ev.chars().take(48).collect::<String>());
                    let already_used = state
                        .db
                        .list_player_storage(&eid)
                        .unwrap_or_default()
                        .into_iter()
                        .any(|(k, _)| k == tut_key);
                    if char_lv > 2 || already_used {
                        continue;
                    }
                    let surv_res = {
                        let mut live = state.live_clients.write().await;
                        live.get_mut(&eid).and_then(|c| {
                            if c.in_battle {
                                return None;
                            }
                            let was_dead = !c.is_alive;
                            match ev.as_str() {
                                "init_health" | "resurrect" => {
                                    c.life = 100.0;
                                    c.stamina = 100.0;
                                    c.stamina_at = now;
                                    c.energy = 100.0;
                                    c.fatigue = 0.0;
                                    c.is_alive = true;
                                    c.last_battle_action_at = 0.0;
                                }
                                "restore_food_k" => {
                                    c.energy = 100.0;
                                }
                                "set_tired" => {
                                    c.fatigue = 75.0;
                                }
                                _ => {}
                            }
                            Some((
                                was_dead,
                                (c.life, c.current_stamina(now), c.energy, c.fatigue),
                                c.to_survival_msg(now),
                            ))
                        })
                    };
                    if let Some((was_dead, (life, stam, energy, fatigue), surv)) = surv_res {
                        let _ = state.db.update_survival(&eid, life, stam, energy, fatigue);
                        let _ = state.db.set_player_storage(&eid, &tut_key, b"1");
                        let sf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_SURVIVAL,
                            &surv.pack()?,
                        )?;
                        let _ = tx.send(sf).await;
                        if was_dead || ev == "resurrect" {
                            let erev = EntityRevivedMsg {
                                entity_id: eid,
                                at: now,
                            };
                            let erev_frame = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                0,
                                TC_ENTITY_REVIVED,
                                &erev.pack()?,
                            )?;
                            state.broadcast_region_all(cur_region, &erev_frame).await;
                        }
                    }
                }
                TC_FIRE_BURNABLE | TC_EXTINGUISH_BURNABLE => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = EntityTileMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    let lit = h.type_code == TC_FIRE_BURNABLE;
                    if let Ok(Some(mut art)) = state.db.get_artifact(&req.entity_id) {
                        if let Err(reason) =
                            can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                                .await
                        {
                            let abort = AbortMsg {
                                text: reason.into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                            continue;
                        }
                        art.is_lit = lit;
                        let _ = state.db.update_artifact_flags(
                            &art.entity_id,
                            art.gate_opened,
                            art.is_lit,
                            &art.materials_json,
                        );
                        let appear = artifact_record_to_appear_msg(&art, &state.catalog, now);
                        let df = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            durango_wire::TC_ARTIFACT_DISPLAY,
                            &appear.display_msg().pack()?,
                        )?;
                        state.broadcast_region_all(&art.region_id, &df).await;
                    state.db.log_audit(
                            now,
                            "BUILD",
                            &authed_name,
                            &peer_ip,
                            &art.region_id,
                            &format!(
                                "{} '{}' at ({}, {})",
                                if lit { "Lit" } else { "Extinguished" },
                                art.blueprint_id,
                                art.tile_x,
                                art.tile_y
                            ),
                        );
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_PUT_MATERIALS_INTO_ARTIFACT => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let cur_region = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let req = PutMaterialsIntoArtifactMsg::unpack(&frame.msgpack)?;
                    let now = ServerState::now_unix_secs();
                    if !is_entity_alive(&state, &eid).await {
                        let abort = AbortMsg {
                            text: "Cannot add materials while unconscious.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let Some(art) = state.db.get_artifact(&req.entity_id).ok().flatten() else {
                        let abort = AbortMsg {
                            text: "Structure not found.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    };
                    if art.building_state != BUILDING_STATE_OCCUPIED
                        && art.building_state != BUILDING_STATE_BUILT
                    {
                        let abort = AbortMsg {
                            text: "Materials can only be added during construction.".into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    if let Err(reason) =
                        can_access_artifact(&state, &eid, &authed_owner, cur_region, &art, false)
                            .await
                    {
                        let abort = AbortMsg {
                            text: reason.into(),
                        };
                        let bytes = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_ABORT,
                            &abort.pack()?,
                        )?;
                        let _ = tx.send(bytes).await;
                        continue;
                    }
                    let player_items = state.db.list_items(&eid).unwrap_or_default();
                    let eq_ids: HashSet<String> = state
                        .db
                        .list_equipped(&eid)
                        .unwrap_or_default()
                        .into_iter()
                        .map(|(_, id)| id)
                        .collect();
                    let mut seen_mats = HashSet::new();
                    let mut consumed_ids = Vec::new();
                    let mut slot_items: BTreeMap<String, Vec<ItemMsg>> = BTreeMap::new();
                    if let Ok(stored) = serde_json::from_str::<BTreeMap<String, Vec<ItemRecord>>>(&art.materials_json) {
                        for (slot, recs) in stored {
                            slot_items.insert(
                                slot,
                                recs.iter()
                                    .map(|r| item_record_to_msg(r, &state.catalog, now))
                                    .collect(),
                            );
                        }
                    }
                    let mut stored_records: BTreeMap<String, Vec<ItemRecord>> =
                        serde_json::from_str(&art.materials_json).unwrap_or_default();
                    for (slot, ids) in &req.materials {
                        let mut added_recs = Vec::new();
                        for iid in ids {
                            if !seen_mats.insert(iid.clone()) {
                                continue;
                            }
                            if let Some(it) = player_items
                                .iter()
                                .find(|i| &i.item_id == iid && !i.locked && !eq_ids.contains(&i.item_id))
                            {
                                consumed_ids.push(it.item_id.clone());
                                added_recs.push(it.clone());
                            }
                        }
                        if !added_recs.is_empty() {
                            let msgs: Vec<ItemMsg> = added_recs
                                .iter()
                                .map(|r| item_record_to_msg(r, &state.catalog, now))
                                .collect();
                            slot_items.entry(slot.clone()).or_default().extend(msgs);
                            stored_records.entry(slot.clone()).or_default().extend(added_recs);
                        }
                    }
                    if !consumed_ids.is_empty()
                        && state.db.delete_items(&eid, &consumed_ids).is_ok()
                    {
                        let mat_json = serde_json::to_string(&stored_records)
                            .unwrap_or_else(|_| art.materials_json.clone());
                        let _ = state.db.update_artifact_flags(
                            &art.entity_id,
                            art.gate_opened,
                            art.is_lit,
                            &mat_json,
                        );
                        let upd = InventoryUpdatedMsg {
                            entity_id: eid.clone(),
                            items: Vec::new(),
                            removed_item_ids: consumed_ids.clone(),
                            item_order: None,
                        };
                        let uf = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            0,
                            TC_INVENTORY_UPDATED,
                            &upd.pack()?,
                        )?;
                        let _ = tx.send(uf).await;
                    }
                    state.db.log_audit(
                        now,
                        "BUILD",
                        &authed_name,
                        &peer_ip,
                        &art.region_id,
                        &format!(
                            "Added {} material(s) to '{}' at ({}, {})",
                            consumed_ids.len(),
                            art.blueprint_id,
                            art.tile_x,
                            art.tile_y
                        ),
                    );
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                    let am = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_ARTIFACT_MATERIALS,
                        &ArtifactMaterialsMsg {
                            entity_id: art.entity_id,
                            materials: slot_items,
                        }
                        .pack()?,
                    )?;
                    let _ = tx.send(am).await;
                }
                TC_MOUNT_AIR_BALLOON => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let _ = MountAirBalloonMsg::unpack(&frame.msgpack);
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let alive = {
                        let mut live = state.live_clients.write().await;
                        if let Some(client) = live.get_mut(&eid) {
                            if !client.is_alive {
                                false
                            } else {
                                client.stop_rest(now);
                                client.boarding_on = BOARDING_ON_AIR_BALLOON;
                                client.boarding_since = Some(now);
                                client.boarding_until = Some(now + 900.0);
                                client.vehicle_entity_id.clear();
                                true
                            }
                        } else {
                            false
                        }
                    };
                    if !alive {
                        if h.seq != 0 {
                            let abort = AbortMsg {
                                text: "Cannot board hot air balloon while unconscious.".into(),
                            };
                            let bytes = encode_frame(
                                now,
                                next_seq.fetch_add(1, Ordering::Relaxed),
                                h.seq,
                                TC_ABORT,
                                &abort.pack()?,
                            )?;
                            let _ = tx.send(bytes).await;
                        }
                        continue;
                    }
                    broadcast_player_display_for(&state, &eid, region_id, now, &next_seq).await?;
                    if h.seq != 0 {
                        let ok = encode_frame(
                            now,
                            next_seq.fetch_add(1, Ordering::Relaxed),
                            h.seq,
                            TC_OK,
                            &pack_empty_struct()?,
                        )?;
                        let _ = tx.send(ok).await;
                    }
                }
                TC_UNMOUNT_AIR_BALLOON => {
                    if !is_ready {
                        continue;
                    }
                    let Some(eid) = authed_entity.clone() else {
                        continue;
                    };
                    let now = ServerState::now_unix_secs();
                    let region_id = authed_region
                        .as_deref()
                        .unwrap_or(crate::state::DEFAULT_REGION_CODE);
                    let changed = {
                        let mut live = state.live_clients.write().await;
                        if let Some(client) = live.get_mut(&eid) {
                            if client.boarding_on == BOARDING_ON_AIR_BALLOON {
                                client.boarding_on = BOARDING_ON_NONE;
                                client.boarding_since = None;
                                client.boarding_until = None;
                                client.vehicle_entity_id.clear();
                                true
                            } else {
                                false
                            }
                        } else {
                            false
                        }
                    };
                    let _ = state.db.set_player_storage(&eid, "_balloon_landed", b"1");
                    if changed {
                        broadcast_player_display_for(&state, &eid, region_id, now, &next_seq)
                            .await?;
                    }
                }
                TC_ACTIVE_PERSONAL_REGION_WARPHOLE
                | TC_SET_RECIPE_LIKE
                | TC_SET_BLUEPRINT_LIKE
                | TC_SELECT_TITLE
                | TC_CANCEL_TARGET_TITLE
                | TC_DISCOVER_ANIMAL => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    if h.type_code == TC_SET_RECIPE_LIKE || h.type_code == TC_SET_BLUEPRINT_LIKE {
                        let _ = SetLikeMsg::unpack(&frame.msgpack);
                    }
                    let ok = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_OK,
                        &pack_empty_struct()?,
                    )?;
                    let _ = tx.send(ok).await;
                }
                TC_GET_SOCIAL => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_SOCIAL,
                        &pack_empty_social()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_MEMOS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_MEMOS,
                        &pack_empty_single_map()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_CLAN_CREATION_COSTS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_COSTS,
                        &pack_empty_single_map()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_FACTIONS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_FACTIONS,
                        &pack_empty_factions()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_MISSIONS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_MISSION_INFOS,
                        &pack_empty_mission_infos()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_TITLES => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_TITLES,
                        &pack_empty_single_array()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ADD_ONS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_ADD_ONS,
                        &pack_empty_single_map()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_MUSICS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_MUSICS,
                        &pack_empty_musics()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_ENCYCLOPEDIA => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_FARMING_ENCYCLOPEDIA,
                        &pack_empty_single_map()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_SUPPORT_REQUESTS => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        0,
                        TC_SUPPORT_REQUESTS,
                        &pack_empty_support_requests()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_NOMAD_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_NOMAD_INFO,
                        &pack_empty_nomad_info()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_RETURNER_INFO => {
                    if !is_ready {
                        continue;
                    }
                    let now = ServerState::now_unix_secs();
                    let bytes = encode_frame(
                        now,
                        next_seq.fetch_add(1, Ordering::Relaxed),
                        h.seq,
                        TC_RETURNER_INFO,
                        &pack_empty_returner_info()?,
                    )?;
                    let _ = tx.send(bytes).await;
                }
                TC_GET_PARTY => {
                    // Client fires GetParty without binding a reply handler; acknowledge quietly.
                }
                other => {
                    tracing::debug!(%peer, type_code = other, payload = frame.msgpack.len(), "TCP unhandled packet type (ignored safely)");
                }
            }
        }
    }
    Ok(())
    }
    .await;

    active_gather_gen.fetch_add(1, Ordering::Relaxed);
    if let Some(cancel_tx) = active_gather_cancel.take() {
        let _ = cancel_tx.send(());
    }
    active_craft_gen.fetch_add(1, Ordering::Relaxed);
    if let Some(cancel_tx) = active_craft_cancel.take() {
        let _ = cancel_tx.send(());
    }
    if let Some(eid) = authed_entity {
        let removed = {
            let mut live = state.live_clients.write().await;
            if live.get(&eid).is_some_and(|c| c.conn_id == conn_id) {
                live.remove(&eid)
            } else {
                None
            }
        };
        if let Some(mut removed) = removed {
            let now = ServerState::now_unix_secs();
            let _ = removed.stop_rest(now);
            if !emigrated {
                let _ = state
                    .db
                    .update_position(&eid, removed.pos_x, removed.pos_y, now);
            }
            let _ = state.db.update_survival(
                &eid,
                removed.life,
                removed.current_stamina(now),
                removed.energy,
                removed.fatigue,
            );
            let disappear = DisappearEntity {
                entity_id: eid.clone(),
            };
            if let Ok(packed) = disappear.pack() {
                if let Ok(frame) = encode_frame(
                    now,
                    next_seq.fetch_add(1, Ordering::Relaxed),
                    0,
                    TC_DISAPPEAR_ENTITY,
                    &packed,
                ) {
                    state
                        .broadcast_region_except(&removed.region_id, &eid, &frame)
                        .await;
                }
            }
            tracing::info!(%peer, entity = %eid, emigrated, "TCP player left world (saved state to SQLite)");
            state.db.log_audit(
                ServerState::now_unix_secs(),
                "WORLD",
                &authed_name,
                &peer_ip,
                authed_region.as_deref().unwrap_or(""),
                if emigrated {
                    "Traveled to another island"
                } else {
                    "Disconnected from world"
                },
            );
        }
    }

    drop(tx);
    let _ = writer_task.await;
    loop_res?;
    Ok(())
}
