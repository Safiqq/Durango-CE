//! Cleanroom Rust implementation of the exact Durango v3.3.6 TCP wire protocol:
//! 24-byte Little-Endian header + raw Snappy block-compressed MessagePack array payload.

use std::collections::BTreeMap;
use std::io::Cursor;

use rmp::decode::{
    read_array_len, read_bin_len, read_bool, read_f32, read_f64, read_i32, read_i64, read_int,
    read_map_len, read_str_len,
};
use rmp::encode::{
    write_array_len, write_bin, write_bool, write_f32, write_f64, write_i32, write_i64,
    write_map_len, write_nil, write_str, write_u16, write_u32, write_u8,
};
use thiserror::Error;

pub const HEADER_SIZE: usize = 24;
pub const MAX_PAYLOAD_SIZE: usize = 524_288 - HEADER_SIZE;
pub const MAX_UNCOMPRESSED_SIZE: usize = 2 * 1024 * 1024;
const MAX_DECODE_PREALLOC: usize = 4096;
const MAX_MP_SKIP_DEPTH: usize = 32;

// Exact v3.3.6 Message TypeCodes
pub const TC_REPLY_SEQUENCE_MARK: u32 = 0;
pub const TC_AUTH: u32 = 1;
pub const TC_MOVE: u32 = 4;
pub const TC_CRAFT: u32 = 6;
pub const TC_ESTIMATE_CRAFT: u32 = 7;
pub const TC_EQUIP: u32 = 10;
pub const TC_INVENTORY_ORDER: u32 = 15;
pub const TC_DUMP_ITEMS: u32 = 16;
pub const TC_DAMAGED: u32 = 12;
pub const TC_USE_ITEM: u32 = 17;
pub const TC_READY: u32 = 20;
pub const TC_WELCOME: u32 = 22;
pub const TC_COMBAT_INTERACTION: u32 = 23;
pub const TC_APPEAR_PLAYER: u32 = 90;
pub const TC_APPEAR_ANIMAL: u32 = 91;
pub const TC_APPEAR_ARTIFACT: u32 = 93;
pub const TC_ARTIFACT_STATE: u32 = 323;
pub const TC_ARTIFACT_DISPLAY: u32 = 2433;
pub const TC_DISAPPEAR_ENTITY: u32 = 101;
pub const TC_COLLECTED: u32 = 104;
pub const TC_MEMBER: u32 = 105;
pub const TC_INVENTORY_ITEMS: u32 = 107;
pub const TC_INVENTORY_INFOS: u32 = 109;
pub const TC_INVENTORY: u32 = 110;
pub const TC_EQUIPMENTS: u32 = 111;
pub const TC_DISAPPEAR_ENTITY_ON_TILE: u32 = 112;
pub const TC_INVENTORY_UPDATED: u32 = 113;
pub const TC_ENTITY_DIED: u32 = 119;
pub const TC_RECIPES: u32 = 120;
pub const TC_CRAFTED: u32 = 122;
pub const TC_SKILLS: u32 = 123;
pub const TC_REVIVED: u32 = 131;
pub const TC_SURVIVAL: u32 = 182;
pub const TC_SET_CHUNK: u32 = 199;
pub const TC_CHUNK: u32 = 200;
pub const TC_DEFOGGED_CHUNKS: u32 = 203;
pub const TC_GET_DEFOGGED_CHUNKS: u32 = 204;
pub const TC_GET_REGION_MAP_INFO: u32 = 205;
pub const TC_REGION_MAP_INFO: u32 = 206;
pub const TC_KEEPALIVE: u32 = 254;
pub const TC_OCCUPIED: u32 = 301;
pub const TC_GET_ACTIONS: u32 = 314;
pub const TC_ACTIONS: u32 = 315;
pub const TC_ARTIFACT_BLUEPRINTS: u32 = 316;
pub const TC_STATUS_EFFECTS: u32 = 317;
pub const TC_FATIGUE_VELOCITIES: u32 = 318;
pub const TC_GET_POI_COUNT: u32 = 900;
pub const TC_POI_COUNT: u32 = 901;
pub const TC_GET_EXPLORED_POIS: u32 = 902;
pub const TC_EXPLORED_POIS: u32 = 903;
pub const TC_GET_LAST_SEARCHED_TIME: u32 = 906;
pub const TC_LAST_SEARCHED_TIME: u32 = 907;
pub const TC_EXPLORE_POI: u32 = 908;
pub const TC_REQUEST_NEAREST_POI: u32 = 911;
pub const TC_NEAREST_POI: u32 = 912;
pub const TC_OPEN_MAP: u32 = 915;
pub const TC_ERROR: u32 = 1022;
pub const TC_ABORT: u32 = 1024;
pub const TC_TIMER: u32 = 1134;
pub const TC_OK: u32 = 1231;
pub const TC_FAILED: u32 = 1232;
pub const TC_TUTORIAL_EVENT: u32 = 701;
pub const TC_SEARCH_POIS: u32 = 904;
pub const TC_SEARCHED_POIS: u32 = 905;
pub const TC_DESTRUCTING: u32 = 2007;
pub const TC_GET_INVENTORY: u32 = 2010;
pub const TC_GET_RECIPES: u32 = 2011;
pub const TC_GET_ARTIFACT_BLUEPRINTS: u32 = 2012;
pub const TC_GET_STATUS_EFFECTS: u32 = 2016;
pub const TC_GET_COLLECTIBLE: u32 = 2017;
pub const TC_GET_ARTIFACT: u32 = 2018;
pub const TC_COLLECTIBLE: u32 = 2019;
pub const TC_TOUCHED: u32 = 2020;
pub const TC_TOUCH: u32 = 2021;
pub const TC_CANCEL_CRAFTING: u32 = 2023;
pub const TC_GET_WORKBENCH: u32 = 2025;
pub const TC_COLLECT: u32 = 2026;
pub const TC_WITHDRAW: u32 = 2028;
pub const TC_TRAVEL_BY_REGION: u32 = 2029;
pub const TC_GET_ROUTES: u32 = 2030;
pub const TC_ROUTES: u32 = 2032;
pub const TC_POINTS: u32 = 2033;
pub const TC_REMOVE_DEATH_POINT: u32 = 2034;
pub const TC_CANCEL: u32 = 2036;
pub const TC_TELEPORTED: u32 = 2037;
pub const TC_CANCELED: u32 = 2038;
pub const TC_GET_STATISTICS: u32 = 2039;
pub const TC_STATISTICS: u32 = 2040;
pub const TC_REGION: u32 = 2041;
pub const TC_PERSONAL_REGION: u32 = 2042;
pub const TC_GET_TITLES: u32 = 2044;
pub const TC_TITLES: u32 = 2045;
pub const TC_SELECT_TITLE: u32 = 2046;
pub const TC_GET_SKILLS: u32 = 2047;
pub const TC_LEARN_SKILL: u32 = 2048;
pub const TC_UNTRAIN_SKILL: u32 = 2049;
pub const TC_DESTRUCT_ARTIFACT: u32 = 2051;
pub const TC_ARCHIPELAGO: u32 = 2053;
pub const TC_TRAVEL_BY_REGION_IN_ARCHIPELAGO: u32 = 2054;
pub const TC_REPAIR_ARTIFACT: u32 = 2055;
pub const TC_REPAIR_IMMEDIATE: u32 = 2056;
pub const TC_OCCUPY_ARTIFACT_SITE: u32 = 2057;
pub const TC_LEVEL_UP_EFFECT: u32 = 2062;
pub const TC_REWARDED: u32 = 2065;
pub const TC_REGISTER_PRODUCT: u32 = 2069;
pub const TC_BUY_PRODUCT: u32 = 2071;
pub const TC_GET_MAILS: u32 = 2072;
pub const TC_MAILS: u32 = 2073;
pub const TC_ACCEPT_MAILS: u32 = 2075;
pub const TC_DELETE_MAILS: u32 = 2076;
pub const TC_SEND_MAIL: u32 = 2077;
pub const TC_TOGGLE_STATUS_EFFECT: u32 = 2081;
pub const TC_GET_CHEAT_FLAGS: u32 = 2088;
pub const TC_CHEAT_FLAGS: u32 = 2089;
pub const TC_BUILD_ARTIFACT: u32 = 2090;
pub const TC_ARTIFACT_MATERIALS: u32 = 2091;
pub const TC_PUT_MATERIALS_INTO_ARTIFACT: u32 = 2092;
pub const TC_ARTIFACT_BUILT: u32 = 2093;
pub const TC_COMPLETE_ARTIFACT: u32 = 2094;
pub const TC_ARTIFACT_COMPLETED: u32 = 2095;
pub const TC_FIRE_BURNABLE: u32 = 2096;
pub const TC_EXTINGUISH_BURNABLE: u32 = 2097;
pub const TC_EMIGRATED: u32 = 2099;
pub const TC_RETURN_TO_HOME: u32 = 2100;
pub const TC_REVIVE: u32 = 2101;
pub const TC_SET_AS_HOME: u32 = 2102;
pub const TC_VISIT_ESTATE: u32 = 2104;
pub const TC_SET_RETURNING_POINT: u32 = 2105;
pub const TC_GET_WARP_COSTS: u32 = 2106;
pub const TC_WARP_COSTS: u32 = 2107;
pub const TC_WARP: u32 = 2108;
pub const TC_GET_REGION: u32 = 2120;
pub const TC_GET_ARCHIPELAGO: u32 = 2121;
pub const TC_SET_STORAGE_ITEM: u32 = 2302;
pub const TC_GET_SOCIAL: u32 = 2402;
pub const TC_SOCIAL: u32 = 2403;
pub const TC_SAY_IN_EXCLUSIVE_CHANNEL: u32 = 2408;
pub const TC_ESTIMATE_BUILD: u32 = 2414;
pub const TC_BUILD_ESTIMATION: u32 = 2415;
pub const TC_REST_ON: u32 = 2416;
pub const TC_COLLECTIBLE_CHANGED: u32 = 2417;
pub const TC_SET_ESTATE_LICENSE: u32 = 2420;
pub const TC_EXPAND_ESTATE: u32 = 2421;
pub const TC_DECLARE_ESTATE: u32 = 2422;
pub const TC_SHRINK_ESTATE: u32 = 2426;
pub const TC_ESTATE_GRIDS: u32 = 2429;
pub const TC_ESTATE_LICENSE: u32 = 2430;
pub const TC_PLAYER_DISPLAY: u32 = 2431;
pub const TC_PUT_IN_ITEM: u32 = 2434;
pub const TC_TAKE_OUT_ITEM: u32 = 2435;
pub const TC_GET_ADD_ONS: u32 = 2437;
pub const TC_ADD_ONS: u32 = 2438;
pub const TC_GET_MEMOS: u32 = 2439;
pub const TC_MEMOS: u32 = 2440;
pub const TC_RESEARCH_SKILL_CATEGORY: u32 = 2446;
pub const TC_DEPART: u32 = 2448;
pub const TC_DASHED: u32 = 2491;
pub const TC_RADIO_ID: u32 = 2600;
pub const TC_RADIO_TALK: u32 = 2601;
pub const TC_WORKBENCH: u32 = 3000;
pub const TC_RECOMMEND_REGION: u32 = 3001;
pub const TC_RECOMMEND_ARCHIPELAGO: u32 = 3012;
pub const TC_IS_WARPHOLE_AVAILABLE: u32 = 3021;
pub const TC_ACTIVE_PERSONAL_REGION_WARPHOLE: u32 = 3022;
pub const TC_WARP_TO_PERSONAL_REGION: u32 = 3023;
pub const TC_GET_SAILING_BACK_COST: u32 = 3110;
pub const TC_SAILING_BACK_COST: u32 = 3120;
pub const TC_SAILING_BACK: u32 = 3130;
pub const TC_BATTLE_BEGUN: u32 = 3278;
pub const TC_USE_BATTLE_ACTION: u32 = 3440;
pub const TC_SELECT_BATTLE_TARGET: u32 = 3441;
pub const TC_DRINK_WATER: u32 = 3492;
pub const TC_WASH_BODY: u32 = 3494;
pub const TC_ENTER_BATTLE: u32 = 3495;
pub const TC_EXIT_BATTLE: u32 = 3496;
pub const TC_LOCK_OR_UNLOCK_ITEMS: u32 = 3497;
pub const TC_BATTLE_ENDED: u32 = 3587;
pub const TC_GET_FACTIONS: u32 = 3600;
pub const TC_FACTIONS: u32 = 3601;
pub const TC_GET_MISSIONS: u32 = 3620;
pub const TC_MISSION_INFOS: u32 = 3622;
pub const TC_MAKE_CLAN: u32 = 3651;
pub const TC_LEAVE_CLAN: u32 = 3652;
pub const TC_GET_CLAN_CREATION_COSTS: u32 = 3667;
pub const TC_GET_WAREHOUSE: u32 = 3683;
pub const TC_WAREHOUSE: u32 = 3684;
pub const TC_MOVE_ITEMS_IN_WAREHOUSE: u32 = 3685;
pub const TC_MAKE_SECTION: u32 = 3686;
pub const TC_REMOVE_SECTION: u32 = 3687;
pub const TC_SET_SECTION_ORDER: u32 = 3688;
pub const TC_SET_SECTION_ITEM_ORDER: u32 = 3689;
pub const TC_ADD_ITEMS_TO_WAREHOUSE: u32 = 3690;
pub const TC_POP_ITEMS_FROM_WAREHOUSE: u32 = 3691;
pub const TC_GET_SECTION_ITEMS: u32 = 3692;
pub const TC_RENAME_WAREHOUSE_SECTION: u32 = 3696;
pub const TC_EXP_GAINED: u32 = 3707;
pub const TC_REPAIR_ITEM: u32 = 3717;
pub const TC_SET_RECIPE_LIKE: u32 = 3800;
pub const TC_SET_BLUEPRINT_LIKE: u32 = 3801;
pub const TC_OPEN_GATE: u32 = 3804;
pub const TC_CLOSE_GATE: u32 = 3805;
pub const TC_GET_ESTATE_LICENSES: u32 = 3820;
pub const TC_ESTATE_LICENSES: u32 = 3821;
pub const TC_EXTEND_ESTATE_ACTIVATION: u32 = 3822;
pub const TC_CANCEL_TARGET_TITLE: u32 = 3901;
pub const TC_GET_CLOCK: u32 = 4000;
pub const TC_CLOCK: u32 = 4001;
pub const TC_COSTS: u32 = 4024;
pub const TC_GET_DISCOVERY_INFO: u32 = 5000;
pub const TC_DISCOVERY_INFO: u32 = 5001;
pub const TC_DISCOVER_ANIMAL: u32 = 5002;
pub const TC_GET_REGISTERED_PRODUCTS: u32 = 5011;
pub const TC_GET_SOLD_PRODUCTS: u32 = 5012;
pub const TC_GET_PURCHASED_PRODUCTS: u32 = 5013;
pub const TC_GET_EXPIRED_PRODUCTS: u32 = 5015;
pub const TC_SEARCH_PRODUCTS: u32 = 5016;
pub const TC_PRODUCTS: u32 = 5100;
pub const TC_MARKET_COLLECT_PAYMENT: u32 = 5101;
pub const TC_MARKET_COLLECT_ALL_PAYMENTS: u32 = 5102;
pub const TC_WASH: u32 = 13_497;
pub const TC_GET_PARTY: u32 = 20_001;
pub const TC_GET_ROUTE_OF_ARCHIPELAGO: u32 = 20_301;
pub const TC_TRAVEL_TO_RANDOM_PERSONAL_REGION: u32 = 20_314;
pub const TC_ROUTES_OF_ARCHIPELAGO: u32 = 20_320;
pub const TC_GET_PERSONAL_REGION_INFO: u32 = 20_420;
pub const TC_PERSONAL_REGION_INFO: u32 = 20_422;
pub const TC_SET_PERSONAL_REGION_ADMISSION: u32 = 20_423;
pub const TC_KICK_VISITOR: u32 = 20_424;
pub const TC_GET_ENCYCLOPEDIA: u32 = 37_125;
pub const TC_FARMING_ENCYCLOPEDIA: u32 = 37_126;
pub const TC_CHANGE_EQUIP_SLOT_TYPE: u32 = 81_534;
pub const TC_REGISTER_MULTIPLE_PRODUCTS: u32 = 82_309;
pub const TC_GET_NOMAD_INFO: u32 = 100_000;
pub const TC_NOMAD_INFO: u32 = 100_001;
pub const TC_ENTITY_REVIVED: u32 = 119_119;
pub const TC_ADD_TO_FAVORITE_PRODUCTS: u32 = 123_482;
pub const TC_MOUNT_AIR_BALLOON: u32 = 123_987;
pub const TC_UNMOUNT_AIR_BALLOON: u32 = 135_867;
pub const TC_GET_FAVORITE_PRODUCTS: u32 = 179_238;
pub const TC_GET_REVIVE_IMMEDIATELY_INFO: u32 = 210_101;
pub const TC_REVIVE_IMMEDIATELY_INFO: u32 = 210_102;
pub const TC_REVIVE_IMMEDIATELY: u32 = 210_201;
pub const TC_GET_QUEST_CATEGORIES: u32 = 237_901;
pub const TC_QUEST_CATEGORIES: u32 = 237_902;
pub const TC_GET_QUESTS: u32 = 237_918;
pub const TC_QUESTS: u32 = 237_919;
pub const TC_CANCEL_FIRED_PROJECTILE: u32 = 312_098;
pub const TC_CRAFT_ESTIMATION_INFO: u32 = 406_132;
pub const TC_REMOVE_FROM_FAVORITE_PRODUCTS: u32 = 423_809;
pub const TC_SUPPORT_REQUESTS: u32 = 523_413;
pub const TC_GET_ESTATE_LICENSE_BY_ID: u32 = 879_534;
pub const TC_GET_SUPPORT_REQUESTS: u32 = 2_347_809;
pub const TC_GET_RETURNER_INFO: u32 = 3_450_983;
pub const TC_RETURN_TO_CAMP: u32 = 3_462_987;
pub const TC_WALLET_UPDATED: u32 = 5_198_234;
pub const TC_RECOMMEND_STABLE_REGIONS: u32 = 5_792_841;
pub const TC_RECOMMENDED_STABLE_REGIONS: u32 = 5_792_842;
pub const TC_RUN_UP: u32 = 7_300_013;
pub const TC_RUN_DOWN: u32 = 7_300_014;
pub const TC_SET_BASE_MOVE_SPEED: u32 = 8_749_871;
pub const TC_WARP_TO_PORT: u32 = 9_081_241;
pub const TC_RETURNER_INFO: u32 = 9_439_833;
pub const TC_REMOVE_ESTATE: u32 = 9_518_234;
pub const TC_GET_AVAILABLE_EMOTIONS: u32 = 9_592_634;
pub const TC_AVAILABLE_EMOTIONS: u32 = 9_592_635;
pub const TC_ACCEPT_USER_MAILS: u32 = 9_786_523;
pub const TC_DELETE_USER_MAILS: u32 = 9_786_524;
pub const TC_GET_ATTACHABLE_ACCESSORIES: u32 = 9_823_457;
pub const TC_ATTACHABLE_ACCESSORIES: u32 = 9_823_458;
pub const TC_RETURN_TO_ESTATE: u32 = 10_190_234;
pub const TC_GET_PETS_INFO: u32 = 14_198_037;
pub const TC_TRAVEL_TO_STABLE_REGION: u32 = 20_321_235;
pub const TC_PETS_INFO: u32 = 29_912_233;
pub const TC_GET_MUSICS: u32 = 47_852_453;
pub const TC_MUSICS: u32 = 47_852_454;
pub const TC_MARK_MAILS_AS_READ: u32 = 98_712_435;
pub const TC_MARK_USER_MAILS_AS_READ: u32 = 98_712_436;
pub const TC_GET_RESISTANCE_EXP_CAPS: u32 = 349_378_781;
pub const TC_RESISTANCE_EXP_CAPS: u32 = 349_378_783;
pub const TC_PIONEER_GRADE_INFO: u32 = 812_234_573;
pub const TC_GET_PIONEER_GRADE_INFO: u32 = 812_234_574;
pub const TC_SET_ARTIFACT_ACCESS: u32 = 987_123_450;

pub const BOARDING_ON_NONE: i32 = 0;
pub const BOARDING_ON_PET: i32 = 1;
pub const BOARDING_ON_AIR_BALLOON: i32 = 2;
pub const BOARDING_ON_VEHICLE: i32 = 3;

// Common Durango Interaction, BuildingState, Battle & Result constants
pub const INTERACTION_ATTACK: i32 = 1;
pub const INTERACTION_BUILD_ARTIFACT: i32 = 101;
pub const INTERACTION_DESTRUCT_ARTIFACT: i32 = 103;
pub const INTERACTION_REPAIR_ARTIFACT: i32 = 108;
pub const INTERACTION_COMPLETE_ARTIFACT: i32 = 110;
pub const INTERACTION_SAILING_ROUTES: i32 = 303;
pub const INTERACTION_SAILING_WITHDRAW: i32 = 305;
pub const INTERACTION_SAILING_RANDOM_PERSONAL: i32 = 310;
pub const INTERACTION_CLOSE_GATE: i32 = 402;
pub const INTERACTION_OPEN_GATE: i32 = 403;
pub const INTERACTION_INVENTORY: i32 = 404;
pub const INTERACTION_REST: i32 = 407;
pub const INTERACTION_SET_AS_HOME: i32 = 410;
pub const INTERACTION_USE_WAREHOUSE: i32 = 415;
pub const INTERACTION_WASH: i32 = 418;
pub const INTERACTION_CRAFT: i32 = 501;
pub const INTERACTION_COLLECT: i32 = 506;
pub const INTERACTION_FIRE: i32 = 512;
pub const INTERACTION_EXTINGUISH: i32 = 513;
pub const INTERACTION_WARP: i32 = 515;
pub const BUILDING_STATE_OCCUPIED: i32 = 0;
pub const BUILDING_STATE_BUILT: i32 = 1;
pub const BUILDING_STATE_COMPLETED: i32 = 2;
pub const DAMAGE_RESULT_HIT: i32 = 0;
pub const DAMAGE_RESULT_EVADE: i32 = 1;
pub const BODY_PART_BODY: i32 = 2;
pub const DAMAGE_DIR_FRONT: i32 = 0;
pub const ATTACK_TYPE_BARE_HANDS: i32 = 0;
pub const ATTACK_TYPE_DAGGER: i32 = 1;
pub const ATTACK_TYPE_SMALL_BODY: i32 = 10;
pub const ATTACK_TYPE_LARGE_BODY: i32 = 11;
pub const DAMAGE_EFFECT_KNOCK_BACK: i32 = 2;
pub const DAMAGE_EFFECT_BLOW: i32 = 4;
pub const MOTION_OPT_NORMAL: u8 = 0;
pub const MOTION_OPT_LOOPING: u8 = 1;
pub const MOTION_OPT_ALIGN_TO_PATH: u8 = 2;
pub const MOTION_OPT_LOOP_ALIGN: u8 = 3;
pub const ANIMAL_STATUS_DEAD: i64 = 8;
pub const ANIMAL_STATUS_BATTLE: i64 = 12;
pub const ANIMAL_STATUS_PEACE: i64 = 19;
pub const ANIMAL_STATUS_ALERT: i64 = 26;
pub const TELEPORT_TYPE_RETURNING: i32 = 1;
pub const TELEPORT_TYPE_WARP: i32 = 2;
pub const TELEPORT_TYPE_REVIVE: i32 = 4;
pub const OWNER_TYPE_PLAYER: i32 = 0;
pub const OWNER_TYPE_CLAN_ESTATE: i32 = 2;
pub const OWNER_TYPE_PERSONAL_PLAYER: i32 = 4;
pub const POI_PORT: i32 = 0;
pub const POI_WARPHOLE: i32 = 1;
pub const POI_CRATER: i32 = 2;
pub const POI_CRACK: i32 = 3;
pub const POI_RIFT: i32 = 6;
pub const REWARD_EFFECT_LEVEL_UP: i32 = 1;
pub const SKILL_CAT_SURVIVAL: i32 = 0;
pub const SKILL_CAT_MELEE: i32 = 2;
pub const SKILL_CAT_RANGED: i32 = 3;
pub const SKILL_CAT_DEFENSE: i32 = 4;
pub const SKILL_CAT_BUTCHERY: i32 = 5;
pub const SKILL_CAT_GATHERING: i32 = 7;
pub const SKILL_CAT_COOKING: i32 = 8;
pub const SKILL_CAT_WEAPONCRAFTING: i32 = 9;
pub const SKILL_CAT_ARMORCRAFTING: i32 = 10;
pub const SKILL_CAT_CONSTRUCTING: i32 = 12;
pub const SKILL_CAT_FARMING: i32 = 13;
pub const SKILL_CAT_PROCESS: i32 = 14;
pub const SKILL_CAT_OIL_POISON: i32 = 15;
pub const RESULT_SUCCESS: i32 = 2;
pub const EQUIP_SLOT_1: i32 = 1;

#[derive(Debug, Error)]
pub enum WireError {
    #[error("payload exceeds maximum size: {0} bytes")]
    PayloadTooLarge(usize),
    #[error("snappy compression failed: {0}")]
    SnappyCompress(String),
    #[error("snappy decompression failed: {0}")]
    SnappyDecompress(String),
    #[error("msgpack encode failed: {0}")]
    Encode(String),
    #[error("msgpack decode failed: {0}")]
    Decode(String),
}

impl From<rmp::encode::ValueWriteError> for WireError {
    fn from(e: rmp::encode::ValueWriteError) -> Self {
        WireError::Encode(e.to_string())
    }
}

impl From<std::io::Error> for WireError {
    fn from(e: std::io::Error) -> Self {
        WireError::Encode(e.to_string())
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct PacketHeader {
    pub time_secs: f64,
    pub seq: u32,
    pub reply_of: u32,
    pub type_code: u32,
    pub payload_size: u32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct RawFrame {
    pub header: PacketHeader,
    /// Uncompressed MessagePack payload bytes.
    pub msgpack: Vec<u8>,
}

/// Encodes a message's uncompressed MessagePack bytes into a complete framed TCP packet
/// (24-byte LE header + raw Snappy block-compressed payload).
pub fn encode_frame(
    time_secs: f64,
    seq: u32,
    reply_of: u32,
    type_code: u32,
    msgpack: &[u8],
) -> Result<Vec<u8>, WireError> {
    let mut encoder = snap::raw::Encoder::new();
    let compressed = encoder
        .compress_vec(msgpack)
        .map_err(|e| WireError::SnappyCompress(e.to_string()))?;
    if compressed.len() > MAX_PAYLOAD_SIZE {
        return Err(WireError::PayloadTooLarge(compressed.len()));
    }
    let time_ms = (time_secs.max(0.0) * 1000.0) as u64;
    let mut out = Vec::with_capacity(HEADER_SIZE + compressed.len());
    out.extend_from_slice(&time_ms.to_le_bytes());
    out.extend_from_slice(&seq.to_le_bytes());
    out.extend_from_slice(&reply_of.to_le_bytes());
    out.extend_from_slice(&type_code.to_le_bytes());
    out.extend_from_slice(&(compressed.len() as u32).to_le_bytes());
    out.extend_from_slice(&compressed);
    Ok(out)
}

/// Attempts to decode one complete frame from `buf`, returning `(frame, bytes_consumed)` if ready,
/// or `Ok(None)` if more TCP bytes are needed.
pub fn try_decode_frame(buf: &[u8]) -> Result<Option<(RawFrame, usize)>, WireError> {
    if buf.len() < HEADER_SIZE {
        return Ok(None);
    }
    let time_ms = u64::from_le_bytes(buf[0..8].try_into().unwrap());
    let seq = u32::from_le_bytes(buf[8..12].try_into().unwrap());
    let reply_of = u32::from_le_bytes(buf[12..16].try_into().unwrap());
    let type_code = u32::from_le_bytes(buf[16..20].try_into().unwrap());
    let payload_size = u32::from_le_bytes(buf[20..24].try_into().unwrap()) as usize;

    if payload_size > MAX_PAYLOAD_SIZE {
        return Err(WireError::PayloadTooLarge(payload_size));
    }
    let total = HEADER_SIZE + payload_size;
    if buf.len() < total {
        return Ok(None);
    }

    let compressed = &buf[HEADER_SIZE..total];
    let dlen = snap::raw::decompress_len(compressed)
        .map_err(|e| WireError::SnappyDecompress(e.to_string()))?;
    if dlen > MAX_UNCOMPRESSED_SIZE {
        return Err(WireError::PayloadTooLarge(dlen));
    }
    let mut decoder = snap::raw::Decoder::new();
    let mut msgpack = vec![0u8; dlen];
    let actual = decoder
        .decompress(compressed, &mut msgpack)
        .map_err(|e| WireError::SnappyDecompress(e.to_string()))?;
    msgpack.truncate(actual);

    let header = PacketHeader {
        time_secs: (time_ms as f64) / 1000.0,
        seq,
        reply_of,
        type_code,
        payload_size: payload_size as u32,
    };
    Ok(Some((RawFrame { header, msgpack }, total)))
}

// ---------------------------------------------------------------------------
// MessagePack helpers matching Durango's exact `Pack(packer, val, hint=false)`
// ---------------------------------------------------------------------------

fn safe_cap(len: usize, cur: &Cursor<&[u8]>) -> usize {
    let rem = cur.get_ref().len().saturating_sub(cur.position() as usize);
    len.min(rem).min(MAX_DECODE_PREALLOC)
}

fn read_mp_string(cur: &mut Cursor<&[u8]>) -> Result<String, WireError> {
    // Handle nil gracefully as empty string
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos < slice.len() && slice[pos] == 0xc0 {
        cur.set_position((pos + 1) as u64);
        return Ok(String::new());
    }
    let len = read_str_len(cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
    let start = cur.position() as usize;
    let end = start
        .checked_add(len)
        .ok_or_else(|| WireError::Decode("string length overflow".into()))?;
    if end > slice.len() {
        return Err(WireError::Decode("truncated string".into()));
    }
    cur.set_position(end as u64);
    Ok(String::from_utf8_lossy(&slice[start..end]).into_owned())
}

fn read_mp_opt_string(cur: &mut Cursor<&[u8]>) -> Result<Option<String>, WireError> {
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos < slice.len() && slice[pos] == 0xc0 {
        cur.set_position((pos + 1) as u64);
        return Ok(None);
    }
    Ok(Some(read_mp_string(cur)?))
}

fn read_mp_opt_f64(cur: &mut Cursor<&[u8]>) -> Result<Option<f64>, WireError> {
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos < slice.len() && slice[pos] == 0xc0 {
        cur.set_position((pos + 1) as u64);
        return Ok(None);
    }
    Ok(Some(read_mp_f64(cur)?))
}

fn read_mp_bin(cur: &mut Cursor<&[u8]>) -> Result<Vec<u8>, WireError> {
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos >= slice.len() {
        return Err(WireError::Decode("eof reading binary".into()));
    }
    let tag = slice[pos];
    if tag == 0xc0 {
        cur.set_position((pos + 1) as u64);
        return Ok(Vec::new());
    }
    // Support both MsgPack v5 `bin` (0xc4..=0xc6) AND Unity MsgPack.dll `Classic`
    // (`PackBinaryAsRaw`, i.e., 0xa0..=0xbf / 0xd9..=0xdb raw string headers).
    let len = match tag {
        0xc4..=0xc6 => read_bin_len(cur).map_err(|e| WireError::Decode(e.to_string()))? as usize,
        0xa0..=0xbf | 0xd9..=0xdb => {
            read_str_len(cur).map_err(|e| WireError::Decode(e.to_string()))? as usize
        }
        _ => return Err(WireError::Decode(format!("invalid binary tag 0x{tag:02x}"))),
    };
    let start = cur.position() as usize;
    let end = start
        .checked_add(len)
        .ok_or_else(|| WireError::Decode("binary length overflow".into()))?;
    if end > slice.len() {
        return Err(WireError::Decode("truncated binary".into()));
    }
    cur.set_position(end as u64);
    Ok(slice[start..end].to_vec())
}

fn read_mp_f64(cur: &mut Cursor<&[u8]>) -> Result<f64, WireError> {
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos >= slice.len() {
        return Err(WireError::Decode("eof reading f64".into()));
    }
    match slice[pos] {
        0xcb => read_f64(cur).map_err(|e| WireError::Decode(e.to_string())),
        0xca => read_f32(cur)
            .map(f64::from)
            .map_err(|e| WireError::Decode(e.to_string())),
        _ => read_int::<i64, _>(cur)
            .map(|v| v as f64)
            .map_err(|e| WireError::Decode(e.to_string())),
    }
}

fn read_mp_f32(cur: &mut Cursor<&[u8]>) -> Result<f32, WireError> {
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos >= slice.len() {
        return Err(WireError::Decode("eof reading f32".into()));
    }
    match slice[pos] {
        0xca => read_f32(cur).map_err(|e| WireError::Decode(e.to_string())),
        0xcb => read_f64(cur)
            .map(|v| v as f32)
            .map_err(|e| WireError::Decode(e.to_string())),
        _ => read_int::<i64, _>(cur)
            .map(|v| v as f32)
            .map_err(|e| WireError::Decode(e.to_string())),
    }
}

fn read_mp_u8(cur: &mut Cursor<&[u8]>) -> Result<u8, WireError> {
    let v: u32 = read_int(cur).map_err(|e| WireError::Decode(e.to_string()))?;
    Ok(v.min(u8::MAX as u32) as u8)
}

fn read_mp_u16(cur: &mut Cursor<&[u8]>) -> Result<u16, WireError> {
    let v: u32 = read_int(cur).map_err(|e| WireError::Decode(e.to_string()))?;
    Ok(v.min(u16::MAX as u32) as u16)
}

fn read_mp_i32(cur: &mut Cursor<&[u8]>) -> Result<i32, WireError> {
    read_int(cur).map_err(|e| WireError::Decode(e.to_string()))
}

fn write_mp_opt_str(buf: &mut Vec<u8>, s: &Option<String>) -> Result<(), WireError> {
    match s {
        Some(v) => write_str(buf, v).map_err(|e| WireError::Encode(e.to_string())),
        None => write_nil(buf).map_err(|e| WireError::Encode(e.to_string())),
    }
}

fn peek_is_nil(cur: &Cursor<&[u8]>) -> bool {
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    pos < slice.len() && slice[pos] == 0xc0
}

fn skip_mp_value(cur: &mut Cursor<&[u8]>) -> Result<(), WireError> {
    skip_mp_value_depth(cur, 0)
}

fn skip_mp_value_depth(cur: &mut Cursor<&[u8]>, depth: usize) -> Result<(), WireError> {
    if depth > MAX_MP_SKIP_DEPTH {
        return Err(WireError::Decode("msgpack nesting too deep".into()));
    }
    let pos = cur.position() as usize;
    let slice = *cur.get_ref();
    if pos >= slice.len() {
        return Err(WireError::Decode("eof skipping msgpack value".into()));
    }
    let advance_checked = |width: usize, cur: &mut Cursor<&[u8]>| -> Result<(), WireError> {
        let end = pos
            .checked_add(width)
            .ok_or_else(|| WireError::Decode("msgpack offset overflow".into()))?;
        if end > slice.len() {
            return Err(WireError::Decode("truncated msgpack value".into()));
        }
        cur.set_position(end as u64);
        Ok(())
    };
    let b = slice[pos];
    match b {
        0x00..=0x7f | 0xe0..=0xff | 0xc0 | 0xc2 | 0xc3 => advance_checked(1, cur)?,
        0xcc | 0xd0 => advance_checked(2, cur)?,
        0xcd | 0xd1 => advance_checked(3, cur)?,
        0xce | 0xd2 | 0xca => advance_checked(5, cur)?,
        0xcf | 0xd3 | 0xcb => advance_checked(9, cur)?,
        0xa0..=0xbf | 0xd9 | 0xda | 0xdb => {
            let _ = read_mp_string(cur)?;
        }
        0xc4 | 0xc5 | 0xc6 => {
            let _ = read_mp_bin(cur)?;
        }
        0x90..=0x9f | 0xdc | 0xdd => {
            let len = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            let rem = slice.len().saturating_sub(cur.position() as usize);
            if len > rem {
                return Err(WireError::Decode("truncated msgpack array".into()));
            }
            for _ in 0..len {
                skip_mp_value_depth(cur, depth + 1)?;
            }
        }
        0x80..=0x8f | 0xde | 0xdf => {
            let len = read_map_len(cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            let rem = slice.len().saturating_sub(cur.position() as usize);
            if len.saturating_mul(2) > rem {
                return Err(WireError::Decode("truncated msgpack map".into()));
            }
            for _ in 0..len {
                skip_mp_value_depth(cur, depth + 1)?;
                skip_mp_value_depth(cur, depth + 1)?;
            }
        }
        _ => return Err(WireError::Decode(format!("unsupported msgpack tag 0x{b:02x}"))),
    }
    Ok(())
}

// ---------------------------------------------------------------------------
// Concrete Durango v3.3.6 Messages
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct GetClock {
    pub time: f64,
}

impl GetClock {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(16);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.time).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let time = read_mp_f64(&mut cur)?;
        Ok(Self { time })
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Clock {
    pub client_time: f64,
    pub server_time: f64,
}

impl Clock {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(24);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.client_time).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.server_time).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let client_time = read_mp_f64(&mut cur)?;
        let server_time = read_mp_f64(&mut cur)?;
        Ok(Self {
            client_time,
            server_time,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Auth {
    pub entity_id: String,
    pub session_token: String,
    pub client_version: String,
    pub device_model: String,
}

impl Auth {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.session_token).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.client_version).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.device_model).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
            session_token: read_mp_string(&mut cur)?,
            client_version: read_mp_string(&mut cur)?,
            device_model: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(i32)]
pub enum RegionRole {
    Sandbox = 0,
    Tutorial = 1,
    Rural = 3,
    Risky = 4,
    Outpost = 5,
    Urban = 6,
    Safehouse = 7,
    Instance = 8,
    Personal = 9,
}

#[derive(Debug, Clone, PartialEq)]
pub struct RegionMsg {
    pub id: String,
    pub terrain_id: String,
    pub template_id: String,
    pub role: RegionRole,
    pub name: Option<String>,
    pub created_at: f64,
}

impl RegionMsg {
    pub fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 6)?;
        write_str(buf, &self.id)?;
        write_str(buf, &self.terrain_id)?;
        write_str(buf, &self.template_id)?;
        write_i32(buf, self.role as i32)?;
        write_mp_opt_str(buf, &self.name)?;
        write_f64(buf, self.created_at)?;
        Ok(())
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        self.write_to(&mut out)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Welcome {
    pub user_id: String,
    pub name: String,
    pub region: RegionMsg,
    pub storage: BTreeMap<String, Vec<u8>>,
    pub bool_options: Vec<(String, bool)>,
    pub int_options: Vec<(String, i64)>,
    pub personal_region_id: Option<String>,
    pub engagement_reward_sent: bool,
}

impl Welcome {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(256);
        // 10 fields: UserId, Name, Region, Storage, Options, Archipelago(nil), PersonalRegionId, Seasons, SocialOptions, EngagementRewardSent
        write_array_len(&mut buf, 10).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.user_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.name).map_err(|e| WireError::Encode(e.to_string()))?;

        // Region (6 fields)
        write_array_len(&mut buf, 6).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.region.id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.region.terrain_id)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.region.template_id)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.region.role as i32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        match &self.region.name {
            Some(n) => write_str(&mut buf, n).map_err(|e| WireError::Encode(e.to_string()))?,
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        write_f64(&mut buf, self.region.created_at)
            .map_err(|e| WireError::Encode(e.to_string()))?;

        // Storage (1 field: map<string, bin>)
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, self.storage.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.storage {
            write_str(&mut buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_bin(&mut buf, v).map_err(|e| WireError::Encode(e.to_string()))?;
        }

        // Options (3 fields: Bool[], Int[], Float[])
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.bool_options.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.bool_options {
            write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_bool(&mut buf, *v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_array_len(&mut buf, self.int_options.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.int_options {
            write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_i64(&mut buf, *v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // Float[]

        // Archipelago? = nil
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;

        // PersonalRegionId
        match &self.personal_region_id {
            Some(pid) => write_str(&mut buf, pid).map_err(|e| WireError::Encode(e.to_string()))?,
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }

        // Seasons (1 field: Season[] empty)
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;

        // SocialOptions (1 field: empty map)
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;

        // EngagementRewardSent
        write_bool(&mut buf, self.engagement_reward_sent)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let user_id = read_mp_string(&mut cur)?;
        let name = read_mp_string(&mut cur)?;

        let _rlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let rid = read_mp_string(&mut cur)?;
        let terrain_id = read_mp_string(&mut cur)?;
        let template_id = read_mp_string(&mut cur)?;
        let role_i32 = read_i32(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let role = match role_i32 {
            0 => RegionRole::Sandbox,
            1 => RegionRole::Tutorial,
            4 => RegionRole::Risky,
            5 => RegionRole::Outpost,
            6 => RegionRole::Urban,
            7 => RegionRole::Safehouse,
            8 => RegionRole::Instance,
            9 => RegionRole::Personal,
            _ => RegionRole::Rural,
        };
        let rname = read_mp_opt_string(&mut cur)?;
        let created_at = read_mp_f64(&mut cur)?;

        let _slen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let map_len = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut storage = BTreeMap::new();
        for _ in 0..map_len {
            let k = read_mp_string(&mut cur)?;
            let v = read_mp_bin(&mut cur)?;
            storage.insert(k, v);
        }

        let _olen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let blen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut bool_options = Vec::with_capacity(safe_cap(blen as usize, &cur));
        for _ in 0..blen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let k = read_mp_string(&mut cur)?;
            let v = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            bool_options.push((k, v));
        }
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut int_options = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let k = read_mp_string(&mut cur)?;
            let v = read_i64(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            int_options.push((k, v));
        }
        let flen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        for _ in 0..flen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let _ = read_mp_string(&mut cur)?;
            let _ = read_mp_f32(&mut cur)?;
        }

        // Archipelago (nil expected)
        let _ = read_mp_opt_string(&mut cur)?;
        let personal_region_id = read_mp_opt_string(&mut cur)?;

        // Seasons
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;

        // SocialOptions
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;

        let engagement_reward_sent =
            read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;

        Ok(Self {
            user_id,
            name,
            region: RegionMsg {
                id: rid,
                terrain_id,
                template_id,
                role,
                name: rname,
                created_at,
            },
            storage,
            bool_options,
            int_options,
            personal_region_id,
            engagement_reward_sent,
        })
    }
}

pub fn pack_empty_struct() -> Result<Vec<u8>, WireError> {
    let mut buf = Vec::with_capacity(2);
    write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
    Ok(buf)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AbortMsg {
    pub text: String,
}

impl AbortMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(self.text.len() + 8);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.text).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct SetChunk {
    pub x: u8,
    pub y: u8,
}

impl SetChunk {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(8);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u8(&mut buf, self.x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u8(&mut buf, self.y).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let x = read_mp_u8(&mut cur)?;
        let y = read_mp_u8(&mut cur)?;
        Ok(Self { x, y })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChunkMsg {
    pub x: u8,
    pub y: u8,
    pub garden: Vec<u8>,
}

impl ChunkMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(16 + self.garden.len());
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u8(&mut buf, self.x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u8(&mut buf, self.y).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bin(&mut buf, &self.garden).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let x = read_mp_u8(&mut cur)?;
        let y = read_mp_u8(&mut cur)?;
        let garden = read_mp_bin(&mut cur)?;
        Ok(Self { x, y, garden })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Location {
    pub x: f32,
    pub y: f32,
    pub yaw: f32,
    pub time: f64,
    pub floor: u8,
    pub height: f32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Movement {
    pub motion_name: String,
    pub motion_option: u8,
    pub playback_rate: f32,
    pub rot_speed: f32,
    pub path: Vec<Location>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct MoveMsg {
    pub entity_id: String,
    pub movements: Vec<Movement>,
}

impl MoveMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.movements.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for m in &self.movements {
            write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, &m.motion_name).map_err(|e| WireError::Encode(e.to_string()))?;
            write_u8(&mut buf, m.motion_option).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f32(&mut buf, m.playback_rate).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f32(&mut buf, m.rot_speed).map_err(|e| WireError::Encode(e.to_string()))?;
            write_array_len(&mut buf, m.path.len() as u32)
                .map_err(|e| WireError::Encode(e.to_string()))?;
            for loc in &m.path {
                write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
                write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
                write_u32(&mut buf, loc.x.round().max(0.0) as u32)
                    .map_err(|e| WireError::Encode(e.to_string()))?;
                write_u32(&mut buf, loc.y.round().max(0.0) as u32)
                    .map_err(|e| WireError::Encode(e.to_string()))?;
                write_f32(&mut buf, loc.yaw).map_err(|e| WireError::Encode(e.to_string()))?;
                write_f64(&mut buf, loc.time).map_err(|e| WireError::Encode(e.to_string()))?;
                write_u8(&mut buf, loc.floor).map_err(|e| WireError::Encode(e.to_string()))?;
                write_f32(&mut buf, loc.height).map_err(|e| WireError::Encode(e.to_string()))?;
            }
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let mlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut movements = Vec::with_capacity(safe_cap(mlen as usize, &cur));
        for _ in 0..mlen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let motion_name = read_mp_string(&mut cur)?;
            let motion_option = read_mp_u8(&mut cur)?;
            let playback_rate = read_mp_f32(&mut cur)?;
            let rot_speed = read_mp_f32(&mut cur)?;
            let plen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut path = Vec::with_capacity(safe_cap(plen as usize, &cur));
            for _ in 0..plen {
                let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                let x = read_mp_f32(&mut cur)?;
                let y = read_mp_f32(&mut cur)?;
                let yaw = read_mp_f32(&mut cur)?;
                let time = read_mp_f64(&mut cur)?;
                let floor = read_mp_u8(&mut cur)?;
                let height = read_mp_f32(&mut cur)?;
                path.push(Location {
                    x,
                    y,
                    yaw,
                    time,
                    floor,
                    height,
                });
            }
            movements.push(Movement {
                motion_name,
                motion_option,
                playback_rate,
                rot_speed,
                path,
            });
        }
        Ok(Self {
            entity_id,
            movements,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DisappearEntity {
    pub entity_id: String,
}

impl DisappearEntity {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(self.entity_id.len() + 8);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct DefoggedChunks {
    pub chunks: Vec<(u8, u8)>,
}

impl DefoggedChunks {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(8 + self.chunks.len() * 4);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.chunks.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for &(x, y) in &self.chunks {
            write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_u8(&mut buf, x).map_err(|e| WireError::Encode(e.to_string()))?;
            write_u8(&mut buf, y).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct GaugeMsg {
    pub max: f32,
    pub min: f32,
    pub nodes: Vec<(f64, f32)>,
}

impl GaugeMsg {
    pub fn constant(now_secs: f64, val: f32, max: f32) -> Self {
        Self {
            max,
            min: 0.0,
            nodes: vec![(now_secs, val)],
        }
    }

    pub fn ramp(now_secs: f64, current: f32, target: f32, rate_per_sec: f32, max: f32) -> Self {
        let clamped_cur = current.clamp(0.0, max);
        let clamped_tgt = target.clamp(0.0, max);
        if (clamped_tgt - clamped_cur).abs() < 0.001 || rate_per_sec.abs() < 0.001 {
            return Self::constant(now_secs, clamped_cur, max);
        }
        let dt = ((clamped_tgt - clamped_cur).abs() / rate_per_sec.abs()) as f64;
        Self {
            max,
            min: 0.0,
            nodes: vec![(now_secs, clamped_cur), (now_secs + dt, clamped_tgt)],
        }
    }

    fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(buf, self.max).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(buf, self.min).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(buf, self.nodes.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for &(t, v) in &self.nodes {
            write_array_len(buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f64(buf, t).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f32(buf, v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(())
    }

    fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let max = read_mp_f32(cur)?;
        let min = read_mp_f32(cur)?;
        let nlen = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut nodes = Vec::with_capacity(safe_cap(nlen as usize, cur));
        for _ in 0..nlen {
            let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let t = read_mp_f64(cur)?;
            let v = read_mp_f32(cur)?;
            nodes.push((t, v));
        }
        Ok(Self { max, min, nodes })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct CharacterAppearance {
    pub is_male: bool,
    pub hair: String,
    pub body: String,
    pub head: Option<String>,
    pub equip: Option<String>,
    pub beard: Option<String>,
    pub body_color: [String; 3],
    pub head_color: [String; 3],
    pub skin_color: String,
    pub hair_color: String,
    pub eye_color: String,
    pub lip_color: String,
    pub portrait: i32,
    pub portrait_bg: i32,
    pub portrait_bg_color: Option<String>,
    pub voice_type: i32,
    pub body_size: f32,
    pub weapon_framework: Option<String>,
    pub boarding_on: i32,
    pub boarding_since: Option<f64>,
    pub boarding_until: Option<f64>,
    pub vehicle_entity_id: String,
}

impl Default for CharacterAppearance {
    fn default() -> Self {
        Self::default_for_gender(true)
    }
}

impl CharacterAppearance {
    pub fn default_for_gender(is_male: bool) -> Self {
        let (body, hair) = if is_male {
            (
                "Models/PC/Male/Body/m_body_nothing.FBX",
                "Models/PC/Male/Hair/m_hair_bear",
            )
        } else {
            (
                "Models/PC/Female/Body/f_body_nothing.FBX",
                "Models/PC/Female/Hair/f_hair_longwave",
            )
        };
        Self {
            is_male,
            hair: hair.into(),
            body: body.into(),
            head: None,
            equip: None,
            beard: None,
            body_color: ["3E5A80".into(), "E6E2D8".into(), "3B3530".into()],
            head_color: ["6E4F3A".into(), "D9C3A3".into(), "3B3530".into()],
            skin_color: "F0C8A0".into(),
            hair_color: "4A3525".into(),
            eye_color: "3B2818".into(),
            lip_color: "C87A7A".into(),
            portrait: 0,
            portrait_bg: 0,
            portrait_bg_color: Some("526A7E".into()),
            voice_type: 0,
            body_size: 0.5,
            weapon_framework: None,
            boarding_on: BOARDING_ON_NONE,
            boarding_since: None,
            boarding_until: None,
            vehicle_entity_id: String::new(),
        }
    }

    pub fn entity_type(&self) -> u16 {
        if self.is_male { 1000 } else { 1001 }
    }

    pub fn default_body(&self) -> &'static str {
        if self.is_male {
            "Models/PC/Male/Body/m_body_nothing.FBX"
        } else {
            "Models/PC/Female/Body/f_body_nothing.FBX"
        }
    }

    pub fn default_inner(&self) -> &'static str {
        if self.is_male {
            "Models/PC/Male/Inner/m_inner_basic.FBX"
        } else {
            "Models/PC/Female/Inner/f_inner_basic.FBX"
        }
    }

    pub fn default_head(&self) -> &'static str {
        if self.is_male {
            "Models/PC/Male/Head/m_head_base1.FBX"
        } else {
            "Models/PC/Female/Head/f_head_base1.FBX"
        }
    }

    pub fn default_hair(&self) -> &'static str {
        if self.is_male {
            "Models/PC/Male/Hair/m_hair_bear"
        } else {
            "Models/PC/Female/Hair/f_hair_longwave"
        }
    }

    pub fn write_player_display(&self, entity_id: &str, buf: &mut Vec<u8>) -> Result<(), WireError> {
        let hair = if self.hair.is_empty() {
            self.default_hair()
        } else {
            self.hair.as_str()
        };
        let body = if self.body.is_empty() {
            self.default_body()
        } else {
            self.body.as_str()
        };
        // Exact 29-field Durango v3.3.6 PlayerDisplay layout
        write_array_len(buf, 29)?;
        write_str(buf, entity_id)?;
        write_str(buf, self.default_body())?;
        write_str(buf, self.default_inner())?;
        write_str(buf, self.default_head())?;
        write_str(buf, self.default_hair())?;
        write_str(buf, hair)?;
        write_str(buf, body)?;
        write_mp_opt_str(buf, &self.head)?;
        write_mp_opt_str(buf, &self.equip)?;
        write_mp_opt_str(buf, &self.beard)?;
        // BodyColor: string[3]
        write_array_len(buf, 3)?;
        for c in &self.body_color {
            write_str(buf, c)?;
        }
        // HeadColor: string[3]
        write_array_len(buf, 3)?;
        for c in &self.head_color {
            write_str(buf, c)?;
        }
        write_nil(buf)?; // EquipColor
        write_str(buf, &self.skin_color)?;
        write_str(buf, &self.hair_color)?;
        write_str(buf, &self.eye_color)?;
        write_str(buf, &self.lip_color)?;
        write_i32(buf, self.portrait)?;
        write_i32(buf, self.portrait_bg)?;
        write_mp_opt_str(buf, &self.portrait_bg_color)?;
        write_nil(buf)?; // PortraitIcon
        write_i32(buf, self.voice_type)?;
        write_f32(buf, self.body_size)?;
        write_bool(buf, false)?; // Invisible
        // WeaponInfo (4 fields: Projectile, ProjectileSpeed, DetonateDelay, WeaponFramework)
        write_array_len(buf, 4)?;
        write_nil(buf)?;
        write_nil(buf)?;
        write_nil(buf)?;
        write_mp_opt_str(buf, &self.weapon_framework)?;
        write_i32(buf, self.boarding_on)?;
        // BoardingTime (Since, Until)
        write_array_len(buf, 2)?;
        match self.boarding_since {
            Some(t) => write_f64(buf, t)?,
            None => write_nil(buf)?,
        }
        match self.boarding_until {
            Some(t) => write_f64(buf, t)?,
            None => write_nil(buf)?,
        }
        write_str(buf, &self.vehicle_entity_id)?;
        write_nil(buf)?; // Accessory
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct AppearPlayerMsg {
    pub entity_id: String,
    pub entity_type: u16,
    pub is_alive: bool,
    pub name: String,
    pub level: i32,
    pub clan_id: String,
    pub clan_name: String,
    pub appearance: CharacterAppearance,
    pub pos_x: f32,
    pub pos_y: f32,
    pub yaw: f32,
    pub now_secs: f64,
}

impl AppearPlayerMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(512);
        // AppearPlayer: 13 fields
        write_array_len(&mut buf, 13).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        let et = if self.entity_type == 1000 || self.entity_type == 1001 {
            self.entity_type
        } else {
            self.appearance.entity_type()
        };
        write_u16(&mut buf, et).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.is_alive).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // Freq
        write_i32(&mut buf, self.level).map_err(|e| WireError::Encode(e.to_string()))?;

        // 7. Title (3 fields: EntityId, TitleId(nil), _Title(""))
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, "").map_err(|e| WireError::Encode(e.to_string()))?;

        // 8. Member (5 fields: EntityId, ClanId, ClanName, RoleId, ApplyingClanId(nil))
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.clan_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.clan_name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, if self.clan_id.is_empty() { 0 } else { 1 })
            .map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;

        // 9. PlayerDisplay (29 fields)
        self.appearance.write_player_display(&self.entity_id, &mut buf)?;

        // 10. Move (2 fields)
        let mv = MoveMsg {
            entity_id: self.entity_id.clone(),
            movements: vec![Movement {
                motion_name: "stand".into(),
                motion_option: 0,
                playback_rate: 1.0,
                rot_speed: 0.0,
                path: vec![Location {
                    x: self.pos_x,
                    y: self.pos_y,
                    yaw: self.yaw,
                    time: self.now_secs,
                    floor: 0,
                    height: 0.0,
                }],
            }],
        };
        let mv_bytes = mv.pack()?;
        buf.extend_from_slice(&mv_bytes);

        // 11. Survival (3 fields: EntityId, Life Gauge, Gauges map)
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        GaugeMsg::constant(self.now_secs, 100.0, 100.0).write_to(&mut buf)?;
        write_map_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, "stamina").map_err(|e| WireError::Encode(e.to_string()))?;
        GaugeMsg::constant(self.now_secs, 100.0, 100.0).write_to(&mut buf)?;
        write_str(&mut buf, "energy").map_err(|e| WireError::Encode(e.to_string()))?;
        GaugeMsg::constant(self.now_secs, 100.0, 100.0).write_to(&mut buf)?;
        write_str(&mut buf, "fatigue").map_err(|e| WireError::Encode(e.to_string()))?;
        GaugeMsg::constant(self.now_secs, 0.0, 100.0).write_to(&mut buf)?;

        // 12. Musician? = nil
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;
        // 13. RescueRequested = false
        write_bool(&mut buf, false).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }
}

// ---------------------------------------------------------------------------
// Step 4 Messages: Inventory, Equipments, Survival, Gathering, Area Chat
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ItemTagMsg {
    pub id: String,
    pub level: i32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct PerformanceMsg {
    pub id: String,
    pub name: String,
    pub icon: String,
    pub nums: BTreeMap<String, f32>,
    pub strs: BTreeMap<String, String>,
}

impl PerformanceMsg {
    pub fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.icon).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(buf, self.nums.len() as u32).map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.nums {
            write_str(buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f32(buf, *v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_map_len(buf, self.strs.len() as u32).map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.strs {
            write_str(buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(buf, v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(())
    }

    pub fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let id = read_mp_string(cur)?;
        let name = read_mp_string(cur)?;
        let icon = read_mp_string(cur)?;
        let nlen = read_map_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut nums = BTreeMap::new();
        for _ in 0..nlen {
            let k = read_mp_string(cur)?;
            let v = read_mp_f32(cur)?;
            nums.insert(k, v);
        }
        let slen = read_map_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut strs = BTreeMap::new();
        for _ in 0..slen {
            let k = read_mp_string(cur)?;
            let v = read_mp_string(cur)?;
            strs.insert(k, v);
        }
        Ok(Self {
            id,
            name,
            icon,
            nums,
            strs,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ItemMsg {
    pub id: String,
    pub name: String,
    pub description: String,
    pub icon: String,
    pub sub_icon: Option<String>,
    pub prototype: String,
    pub level: i32,
    pub original_level: i32,
    pub size: i32,
    pub durability: GaugeMsg,
    pub founder_id: String,
    pub tags: Vec<ItemTagMsg>,
    pub performances: Vec<PerformanceMsg>,
    pub collectible_id: Option<String>,
    pub generator_id: Option<String>,
    pub tradable: bool,
}

impl ItemMsg {
    pub fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        // Exact 29-field Durango v3.3.6 Item layout
        write_array_len(buf, 29).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.description).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.icon).map_err(|e| WireError::Encode(e.to_string()))?;
        write_mp_opt_str(buf, &self.sub_icon)?;
        write_str(buf, &self.prototype).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, self.level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, self.original_level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // ModifiableCount
        write_i32(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // ModifiedCount
        write_i32(buf, self.size.max(1)).map_err(|e| WireError::Encode(e.to_string()))?;
        self.durability.write_to(buf)?;
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // ColorR
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // ColorG
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // ColorB
        write_bool(buf, false).map_err(|e| WireError::Encode(e.to_string()))?; // Unstable
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // RepairRequirement
        write_str(buf, &self.founder_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // FounderCategory
        write_array_len(buf, self.tags.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?; // Tags
        for tag in &self.tags {
            write_array_len(buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(buf, &tag.id).map_err(|e| WireError::Encode(e.to_string()))?;
            write_i32(buf, tag.level).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_array_len(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // TagModifications
        write_array_len(buf, self.performances.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?; // Performance
        for perf in &self.performances {
            perf.write_to(buf)?;
        }
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Ext
        write_mp_opt_str(buf, &self.collectible_id)?;
        write_mp_opt_str(buf, &self.generator_id)?;
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // EmotionalMotions
        write_f32(buf, 0.0).map_err(|e| WireError::Encode(e.to_string()))?; // PioneerCost
        write_bool(buf, self.tradable).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // ReformSlots
        Ok(())
    }

    pub fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let id = read_mp_string(cur)?;
        let name = read_mp_string(cur)?;
        let description = read_mp_string(cur)?;
        let icon = read_mp_string(cur)?;
        let sub_icon = read_mp_opt_string(cur)?;
        let prototype = read_mp_string(cur)?;
        let level = read_mp_i32(cur)?;
        let original_level = read_mp_i32(cur)?;
        let _modifiable_count = read_mp_i32(cur)?;
        let _modified_count = read_mp_i32(cur)?;
        let size = read_mp_i32(cur)?;
        let durability = GaugeMsg::read_from(cur)?;
        let _ = skip_mp_value(cur)?; // ColorR
        let _ = skip_mp_value(cur)?; // ColorG
        let _ = skip_mp_value(cur)?; // ColorB
        let _unstable = read_bool(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = skip_mp_value(cur)?; // RepairRequirement
        let founder_id = read_mp_string(cur)?;
        let _ = skip_mp_value(cur)?; // FounderCategory
        let tag_len = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut tags = Vec::with_capacity(safe_cap(tag_len as usize, cur));
        for _ in 0..tag_len {
            let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let tid = read_mp_string(cur)?;
            let tlv = read_mp_i32(cur)?;
            tags.push(ItemTagMsg { id: tid, level: tlv });
        }
        let _ = skip_mp_value(cur)?; // TagModifications
        let perf_len = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut performances = Vec::with_capacity(safe_cap(perf_len as usize, cur));
        for _ in 0..perf_len {
            performances.push(PerformanceMsg::read_from(cur)?);
        }
        let _ = skip_mp_value(cur)?; // Ext
        let collectible_id = read_mp_opt_string(cur)?;
        let generator_id = read_mp_opt_string(cur)?;
        let _ = skip_mp_value(cur)?; // EmotionalMotions
        let _pioneer_cost = read_mp_f32(cur)?;
        let tradable = read_bool(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = skip_mp_value(cur)?; // ReformSlots
        Ok(Self {
            id,
            name,
            description,
            icon,
            sub_icon,
            prototype,
            level,
            original_level,
            size,
            durability,
            founder_id,
            tags,
            performances,
            collectible_id,
            generator_id,
            tradable,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct InventoryInfosMsg {
    pub entity_id: String,
    pub max_size: i32,
    pub locked_item_ids: Vec<String>,
    pub item_order: Vec<String>,
}

impl InventoryInfosMsg {
    pub fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, self.max_size).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(buf, self.locked_item_ids.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for id in &self.locked_item_ids {
            write_str(buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_array_len(buf, self.item_order.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for id in &self.item_order {
            write_str(buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        // ProtectedItems (1 field: string[] empty)
        write_array_len(buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(())
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        self.write_to(&mut buf)?;
        Ok(buf)
    }

    pub fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(cur)?;
        let max_size = read_mp_i32(cur)?;
        let llen = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut locked_item_ids = Vec::with_capacity(safe_cap(llen as usize, cur));
        for _ in 0..llen {
            locked_item_ids.push(read_mp_string(cur)?);
        }
        let olen = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut item_order = Vec::with_capacity(safe_cap(olen as usize, cur));
        for _ in 0..olen {
            item_order.push(read_mp_string(cur)?);
        }
        let _ = skip_mp_value(cur)?; // ProtectedItems
        Ok(Self {
            entity_id,
            max_size,
            locked_item_ids,
            item_order,
        })
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        Self::read_from(&mut cur)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct InventoryMsg {
    pub entity_id: String,
    pub items: Vec<ItemMsg>,
    pub infos: InventoryInfosMsg,
    pub t_stone: i64,
}

impl InventoryMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128 + self.items.len() * 128);
        // Inventory: 4 fields (EntityId, InventoryItems, InventoryInfos, Wallet)
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;

        // InventoryItems (2 fields: EntityId, Items[])
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.items.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for item in &self.items {
            item.write_to(&mut buf)?;
        }

        // InventoryInfos
        self.infos.write_to(&mut buf)?;

        // Wallet (3 fields: PaidBalances{}, UnpaidBalances{TStone: bal}, Vouchers[])
        write_wallet(&mut buf, self.t_stone)?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _items_eid = read_mp_string(&mut cur)?;
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut items = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            items.push(ItemMsg::read_from(&mut cur)?);
        }
        let infos = InventoryInfosMsg::read_from(&mut cur)?;
        let t_stone = read_wallet_tstone(&mut cur).unwrap_or(0);
        Ok(Self {
            entity_id,
            items,
            infos,
            t_stone,
        })
    }
}

fn write_wallet(buf: &mut Vec<u8>, t_stone: i64) -> Result<(), WireError> {
    write_array_len(buf, 3)?;
    write_map_len(buf, 0)?; // PaidBalances (zero-paywall)
    if t_stone > 0 {
        write_map_len(buf, 1)?;
        write_i32(buf, 0)?; // Currency::TStone = 0
        write_i64(buf, t_stone)?;
    } else {
        write_map_len(buf, 0)?;
    }
    write_array_len(buf, 0)?; // Vouchers
    Ok(())
}

fn read_wallet_tstone(cur: &mut Cursor<&[u8]>) -> Result<i64, WireError> {
    let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
    let _ = skip_mp_value(cur)?; // PaidBalances
    let mut bal = 0i64;
    if !peek_is_nil(cur) {
        let mlen = read_map_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        for _ in 0..mlen {
            let k = read_mp_i32(cur)?;
            let v: i64 = read_int(cur).map_err(|e| WireError::Decode(e.to_string()))?;
            if k == 0 {
                bal = v;
            }
        }
    } else {
        skip_mp_value(cur)?;
    }
    let _ = skip_mp_value(cur)?; // Vouchers
    Ok(bal)
}

#[derive(Debug, Clone, PartialEq)]
pub struct EquipmentsMsg {
    pub current_type: i32,
    pub slots: BTreeMap<String, ItemMsg>,
}

impl EquipmentsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        // Equipments: 2 fields (CurrentType, Presets map<EquipSlotType, EquipmentSlot>)
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.current_type).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.current_type).map_err(|e| WireError::Encode(e.to_string()))?;
        // EquipmentSlot: 5 fields (ItemSlots, IsLocked, UnlockSince=nil, UnlockUntil=nil, TitleId="")
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, self.slots.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (slot_name, item) in &self.slots {
            write_str(&mut buf, slot_name).map_err(|e| WireError::Encode(e.to_string()))?;
            item.write_to(&mut buf)?;
        }
        write_bool(&mut buf, false).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, "").map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let current_type = read_mp_i32(&mut cur)?;
        let plen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut slots = BTreeMap::new();
        for _ in 0..plen {
            let slot_type = read_mp_i32(&mut cur)?;
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut preset_slots = BTreeMap::new();
            for _ in 0..mlen {
                let k = read_mp_string(&mut cur)?;
                let it = ItemMsg::read_from(&mut cur)?;
                preset_slots.insert(k, it);
            }
            let _is_locked = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let _ = skip_mp_value(&mut cur)?; // UnlockSince
            let _ = skip_mp_value(&mut cur)?; // UnlockUntil
            let _ = skip_mp_value(&mut cur)?; // TitleId
            if slot_type == current_type {
                slots = preset_slots;
            }
        }
        Ok(Self {
            current_type,
            slots,
        })
    }
}

// ---------------------------------------------------------------------------
// Step 5 Messages: Equip, PlayerDisplay, UseItem, InventoryOrder,
// InventoryUpdated, Craft, Crafted
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EquipMsg {
    pub slot_name: String,
    pub slot_type: i32,
    pub item_id: String,
    pub action: String,
}

impl EquipMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.slot_name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.slot_type).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.item_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.action).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            slot_name: read_mp_string(&mut cur)?,
            slot_type: read_mp_i32(&mut cur)?,
            item_id: read_mp_string(&mut cur)?,
            action: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct PlayerDisplayMsg {
    pub entity_id: String,
    pub equip: Option<String>,
    pub head: Option<String>,
    pub appearance: CharacterAppearance,
}

impl PlayerDisplayMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(256);
        let mut app = self.appearance.clone();
        if self.head.is_some() {
            app.head = self.head.clone();
        }
        if self.equip.is_some() {
            app.equip = self.equip.clone();
        }
        app.write_player_display(&self.entity_id, &mut buf)?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let arr_len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        for _ in 0..6 {
            let _ = skip_mp_value(&mut cur)?;
        }
        let head = read_mp_opt_string(&mut cur)?;
        let equip = read_mp_opt_string(&mut cur)?;
        let mut appearance = CharacterAppearance::default();
        if arr_len >= 28 {
            for _ in 9..25 {
                let _ = skip_mp_value(&mut cur)?;
            }
            appearance.boarding_on = read_mp_i32(&mut cur)?;
            let bt_len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            if bt_len >= 2 {
                appearance.boarding_since = read_mp_opt_f64(&mut cur)?;
                appearance.boarding_until = read_mp_opt_f64(&mut cur)?;
            } else {
                for _ in 0..bt_len {
                    let _ = skip_mp_value(&mut cur)?;
                }
            }
            appearance.vehicle_entity_id = read_mp_string(&mut cur)?;
        }
        Ok(Self {
            entity_id,
            equip,
            head,
            appearance,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct UseItemMsg {
    pub item_id: String,
    pub accept: bool,
}

impl UseItemMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.item_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.accept).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            item_id: read_mp_string(&mut cur)?,
            accept: read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct InventoryOrderMsg {
    pub is_artifact_target: bool,
    pub item_order: Vec<String>,
}

impl InventoryOrderMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // TargetArtifact = nil
        write_array_len(&mut buf, self.item_order.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for id in &self.item_order {
            write_str(&mut buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let is_artifact_target = !peek_is_nil(&cur);
        skip_mp_value(&mut cur)?;
        let olen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut item_order = Vec::with_capacity(safe_cap(olen as usize, &cur));
        for _ in 0..olen {
            item_order.push(read_mp_string(&mut cur)?);
        }
        Ok(Self {
            is_artifact_target,
            item_order,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct InventoryUpdatedMsg {
    pub entity_id: String,
    pub items: Vec<ItemMsg>,
    pub removed_item_ids: Vec<String>,
    pub item_order: Option<Vec<String>>,
}

impl InventoryUpdatedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(96 + self.items.len() * 128);
        // InventoryUpdated: 5 fields (EntityId, Items, RemovedItemIds, ItemOrder?, ProtectedItems?)
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.items.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for item in &self.items {
            item.write_to(&mut buf)?;
        }
        write_array_len(&mut buf, self.removed_item_ids.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for id in &self.removed_item_ids {
            write_str(&mut buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        match &self.item_order {
            Some(order) => {
                write_array_len(&mut buf, order.len() as u32)
                    .map_err(|e| WireError::Encode(e.to_string()))?;
                for id in order {
                    write_str(&mut buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
                }
            }
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // ProtectedItems?
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut items = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            items.push(ItemMsg::read_from(&mut cur)?);
        }
        let rlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut removed_item_ids = Vec::with_capacity(safe_cap(rlen as usize, &cur));
        for _ in 0..rlen {
            removed_item_ids.push(read_mp_string(&mut cur)?);
        }
        let item_order = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            let olen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut order = Vec::with_capacity(safe_cap(olen as usize, &cur));
            for _ in 0..olen {
                order.push(read_mp_string(&mut cur)?);
            }
            Some(order)
        };
        let _ = skip_mp_value(&mut cur)?; // ProtectedItems?
        Ok(Self {
            entity_id,
            items,
            removed_item_ids,
            item_order,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CraftMsg {
    pub recipe_id: String,
    pub materials: BTreeMap<String, Vec<String>>,
    pub tool_item_id: Option<String>,
    pub reform_slot_index: Option<i32>,
}

impl CraftMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(96);
        // Craft: 5 fields (RecipeId, Materials, ToolItemId?, Workbench?, ReformSlotIndex?)
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.recipe_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, self.materials.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (slot_id, ids) in &self.materials {
            write_str(&mut buf, slot_id).map_err(|e| WireError::Encode(e.to_string()))?;
            write_array_len(&mut buf, ids.len() as u32)
                .map_err(|e| WireError::Encode(e.to_string()))?;
            for id in ids {
                write_str(&mut buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
            }
        }
        write_mp_opt_str(&mut buf, &self.tool_item_id)?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Workbench?
        match self.reform_slot_index {
            Some(idx) => write_i32(&mut buf, idx).map_err(|e| WireError::Encode(e.to_string()))?,
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let recipe_id = read_mp_string(&mut cur)?;
        let mlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut materials = BTreeMap::new();
        for _ in 0..mlen {
            let slot_id = read_mp_string(&mut cur)?;
            let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut ids = Vec::with_capacity(safe_cap(ilen as usize, &cur));
            for _ in 0..ilen {
                ids.push(read_mp_string(&mut cur)?);
            }
            materials.insert(slot_id, ids);
        }
        let tool_item_id = read_mp_opt_string(&mut cur)?;
        let _ = skip_mp_value(&mut cur)?; // Workbench?
        let reform_slot_index = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_i32(&mut cur)?)
        };
        Ok(Self {
            recipe_id,
            materials,
            tool_item_id,
            reform_slot_index,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct CraftedMsg {
    pub result: i32,
    pub action_level: i32,
    pub potential_level: i32,
    pub success_ratio: f32,
    pub items: Vec<ItemMsg>,
}

impl CraftedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64 + self.items.len() * 128);
        // Crafted: 3 fields (Result, ActionInfo(5 fields), Items[])
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.result).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.action_level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.potential_level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, -1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(&mut buf, self.success_ratio).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, -1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.items.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for item in &self.items {
            item.write_to(&mut buf)?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let result = read_mp_i32(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let action_level = read_mp_i32(&mut cur)?;
        let potential_level = read_mp_i32(&mut cur)?;
        let _related_cat = read_mp_i32(&mut cur)?;
        let success_ratio = read_mp_f32(&mut cur)?;
        let _related_ab = read_mp_i32(&mut cur)?;
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut items = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            items.push(ItemMsg::read_from(&mut cur)?);
        }
        Ok(Self {
            result,
            action_level,
            potential_level,
            success_ratio,
            items,
        })
    }
}

// ---------------------------------------------------------------------------
// Step 6 Messages: World Building & Artifacts
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OccupyArtifactSiteMsg {
    pub blueprint_id: Option<String>,
    pub item_id: Option<String>,
    pub tile_x: u16,
    pub tile_y: u16,
    pub floor: Option<i32>,
    pub size_x: u16,
    pub size_y: u16,
    pub stories: Option<i32>,
    pub rotation: i32,
    pub modular_entity_id: String,
}

impl OccupyArtifactSiteMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        // OccupyArtifactSite: 8 fields
        write_array_len(&mut buf, 8).map_err(|e| WireError::Encode(e.to_string()))?;
        write_mp_opt_str(&mut buf, &self.blueprint_id)?;
        write_mp_opt_str(&mut buf, &self.item_id)?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        match self.floor {
            Some(v) => write_i32(&mut buf, v).map_err(|e| WireError::Encode(e.to_string()))?,
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.size_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.size_y).map_err(|e| WireError::Encode(e.to_string()))?;
        match self.stories {
            Some(v) => write_i32(&mut buf, v).map_err(|e| WireError::Encode(e.to_string()))?,
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        write_i32(&mut buf, self.rotation).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.modular_entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let blueprint_id = read_mp_opt_string(&mut cur)?;
        let item_id = read_mp_opt_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let floor = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_i32(&mut cur)?)
        };
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let size_x = read_mp_u16(&mut cur)?;
        let size_y = read_mp_u16(&mut cur)?;
        let stories = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_i32(&mut cur)?)
        };
        let rotation = read_mp_i32(&mut cur)?;
        let modular_entity_id = read_mp_string(&mut cur)?;
        Ok(Self {
            blueprint_id,
            item_id,
            tile_x,
            tile_y,
            floor,
            size_x,
            size_y,
            stories,
            rotation,
            modular_entity_id,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OccupiedMsg {
    pub entity_id: String,
    pub tile_x: i32,
    pub tile_y: i32,
    pub floor: Option<i32>,
}

impl OccupiedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        match self.floor {
            Some(v) => write_i32(&mut buf, v).map_err(|e| WireError::Encode(e.to_string()))?,
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let tile_x = read_mp_i32(&mut cur)?;
        let tile_y = read_mp_i32(&mut cur)?;
        let floor = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_i32(&mut cur)?)
        };
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            floor,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ArtifactDisplayMsg {
    pub entity_id: String,
    pub condition: i32,
    pub parts: BTreeMap<String, String>,
}

impl ArtifactDisplayMsg {
    pub fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 15).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, self.condition).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Color
        write_map_len(buf, self.parts.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?; // Parts
        for (k, v) in &self.parts {
            write_str(buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(buf, v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_map_len(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // Textures
        write_map_len(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // Decorations
        write_map_len(buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // AddOns
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Crop
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // PetEntityTypes
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Effect
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Yaw
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // IndoorColor
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Music
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // Animations
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // MannequinInfo
        Ok(())
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128);
        self.write_to(&mut buf)?;
        Ok(buf)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ArtifactStateMsg {
    pub entity_id: String,
    pub durability: f32,
    pub max_durability: f32,
    pub building_state: i32,
    pub level: u8,
    pub now_secs: f64,
    pub gate_opened: bool,
}

impl ArtifactStateMsg {
    pub fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        let max_d = self.max_durability.max(1.0);
        let cur_d = self.durability.clamp(0.0, max_d);
        write_array_len(buf, 29).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        GaugeMsg::constant(self.now_secs, cur_d, max_d).write_to(buf)?; // Durability
        write_i32(buf, self.building_state).map_err(|e| WireError::Encode(e.to_string()))?;
        GaugeMsg::constant(self.now_secs, 0.0, 0.0).write_to(buf)?; // RepairImmediateCost
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // 5 Repairement
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // 6 Postprocess
        write_bool(buf, self.gate_opened).map_err(|e| WireError::Encode(e.to_string()))?; // 7 GateOpened
        for _ in 8..=23 {
            write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_u8(buf, self.level.max(1)).map_err(|e| WireError::Encode(e.to_string()))?; // 24 Level
        write_f32(buf, max_d).map_err(|e| WireError::Encode(e.to_string()))?; // 25 MaxHealth
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // 26 ChangedName
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // 27 Sprinkler
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // 28 Warpaccelerator
        write_nil(buf).map_err(|e| WireError::Encode(e.to_string()))?; // 29 Bandstand
        Ok(())
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128);
        self.write_to(&mut buf)?;
        Ok(buf)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct AppearArtifactMsg {
    pub entity_id: String,
    pub entity_type: u16,
    pub is_alive: bool,
    pub tile_x: u16,
    pub tile_y: u16,
    pub size_x: u16,
    pub size_y: u16,
    pub height: i32,
    pub rotation: i32,
    pub building_state: i32,
    pub founder_entity_id: String,
    pub level: u8,
    pub now_secs: f64,
    pub parts: BTreeMap<String, String>,
    pub condition: i32,
    pub durability: f32,
    pub max_durability: f32,
    pub gate_opened: bool,
    pub tags: Vec<ItemTagMsg>,
}

impl AppearArtifactMsg {
    pub fn display_msg(&self) -> ArtifactDisplayMsg {
        ArtifactDisplayMsg {
            entity_id: self.entity_id.clone(),
            condition: self.condition,
            parts: self.parts.clone(),
        }
    }

    pub fn state_msg(&self) -> ArtifactStateMsg {
        self.state_msg_with_gate(self.gate_opened)
    }

    pub fn state_msg_with_gate(&self, gate_opened: bool) -> ArtifactStateMsg {
        ArtifactStateMsg {
            entity_id: self.entity_id.clone(),
            durability: self.durability,
            max_durability: self.max_durability,
            building_state: self.building_state,
            level: self.level,
            now_secs: self.now_secs,
            gate_opened,
        }
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(256);
        // Exact 14-field Durango v3.3.6 AppearArtifact layout
        write_array_len(&mut buf, 14).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.entity_type).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.is_alive).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.size_x.max(1)).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.size_y.max(1)).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.height).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Floor
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Stories
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // HasRoof
        write_i32(&mut buf, self.rotation).map_err(|e| WireError::Encode(e.to_string()))?;

        // 11. ArtifactDisplay (15 fields)
        self.display_msg().write_to(&mut buf)?;

        // 12. Tags (2 fields: EntityId, Tag[])
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.tags.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for tag in &self.tags {
            write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, &tag.id).map_err(|e| WireError::Encode(e.to_string()))?;
            write_i32(&mut buf, tag.level).map_err(|e| WireError::Encode(e.to_string()))?;
        }

        // 13. ArtifactState (29 fields)
        self.state_msg().write_to(&mut buf)?;

        // 14. FounderEntityId
        write_str(&mut buf, &self.founder_entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let entity_type = read_mp_u16(&mut cur)?;
        let is_alive = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let size_x = read_mp_u16(&mut cur)?;
        let size_y = read_mp_u16(&mut cur)?;
        let height = read_mp_i32(&mut cur)?;
        let _ = skip_mp_value(&mut cur)?; // Floor
        let _ = skip_mp_value(&mut cur)?; // Stories
        let _ = skip_mp_value(&mut cur)?; // HasRoof
        let rotation = read_mp_i32(&mut cur)?;
        // ArtifactDisplay (15 fields)
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _disp_eid = read_mp_string(&mut cur)?;
        let condition = read_mp_i32(&mut cur)?;
        let _ = skip_mp_value(&mut cur)?; // Color
        let plen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut parts = BTreeMap::new();
        for _ in 0..plen {
            let pk = read_mp_string(&mut cur)?;
            let pv = read_mp_string(&mut cur)?;
            parts.insert(pk, pv);
        }
        for _ in 5..=15 {
            let _ = skip_mp_value(&mut cur)?;
        }
        // Tags [EntityId, Tag[]]
        let mut tags = Vec::new();
        if !peek_is_nil(&cur) {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let _tag_eid = read_mp_string(&mut cur)?;
            let tlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            for _ in 0..tlen {
                let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                let id = read_mp_string(&mut cur)?;
                let level = read_mp_i32(&mut cur)?;
                tags.push(ItemTagMsg { id, level });
            }
        } else {
            let _ = skip_mp_value(&mut cur)?;
        }
        // ArtifactState
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _state_eid = read_mp_string(&mut cur)?;
        let dur = GaugeMsg::read_from(&mut cur)?;
        let building_state = read_mp_i32(&mut cur)?;
        for _ in 4..=6 {
            let _ = skip_mp_value(&mut cur)?;
        }
        let gate_opened = read_bool(&mut cur).unwrap_or(false);
        for _ in 8..=23 {
            let _ = skip_mp_value(&mut cur)?;
        }
        let level = read_mp_u8(&mut cur)?;
        for _ in 25..=29 {
            let _ = skip_mp_value(&mut cur)?;
        }
        let founder_entity_id = read_mp_string(&mut cur)?;
        let now_secs = dur.nodes.first().map_or(0.0, |&(t, _)| t);
        let durability = dur.nodes.first().map_or(dur.max, |&(_, v)| v);
        Ok(Self {
            entity_id,
            entity_type,
            is_alive,
            tile_x,
            tile_y,
            size_x,
            size_y,
            height,
            rotation,
            building_state,
            founder_entity_id,
            level,
            now_secs,
            parts,
            condition,
            durability,
            max_durability: dur.max,
            gate_opened,
            tags,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BuildArtifactMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub tool_item_id: String,
}

impl BuildArtifactMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.tool_item_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let tool_item_id = read_mp_string(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            tool_item_id,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArtifactBuiltMsg {
    pub entity_id: String,
    pub builder_id: String,
}

impl ArtifactBuiltMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.builder_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
            builder_id: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EntityTileMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
}

impl EntityTileMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
        })
    }
}

pub type CompleteArtifactMsg = EntityTileMsg;
pub type DestructArtifactMsg = EntityTileMsg;
pub type RestOnMsg = EntityTileMsg;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArtifactCompletedMsg {
    pub entity_id: String,
}

impl ArtifactCompletedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct DestructingMsg {
    pub duration: f32,
    pub tool_type: u8,
}

impl DestructingMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(12);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(&mut buf, self.duration).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u8(&mut buf, self.tool_type).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            duration: read_mp_f32(&mut cur)?,
            tool_type: read_mp_u8(&mut cur)?,
        })
    }
}

// ---------------------------------------------------------------------------
// Step 7 Messages: Wildlife, Combat & Hunting
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq)]
pub struct AppearAnimalMsg {
    pub entity_id: String,
    pub entity_type: u16,
    pub is_alive: bool,
    pub base_scale: f32,
    pub level: i32,
    pub motion_name: String,
    pub motion_option: u8,
    pub pos_x: f32,
    pub pos_y: f32,
    pub yaw: f32,
    pub hp: f32,
    pub max_hp: f32,
    pub role: Option<String>,
    pub enemy_id: Option<String>,
    pub now_secs: f64,
}

impl AppearAnimalMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(256);
        // Exact 9-field Durango v3.3.6 AppearAnimal layout
        write_array_len(&mut buf, 9).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.entity_type).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.is_alive).map_err(|e| WireError::Encode(e.to_string()))?;

        // 4. AnimalDisplay (3 fields: EntityId, BaseScale, CollectibleDisplay?=nil)
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(&mut buf, self.base_scale).map_err(|e| WireError::Encode(e.to_string()))?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;

        // 5. Level
        write_i32(&mut buf, self.level).map_err(|e| WireError::Encode(e.to_string()))?;

        // 6. Move (2 fields) — real Unity AnimationClip name + looping option
        let mv = MoveMsg {
            entity_id: self.entity_id.clone(),
            movements: vec![Movement {
                motion_name: self.motion_name.clone(),
                motion_option: self.motion_option,
                playback_rate: 1.0,
                rot_speed: 360.0,
                path: vec![Location {
                    x: self.pos_x,
                    y: self.pos_y,
                    yaw: self.yaw,
                    time: self.now_secs,
                    floor: 0,
                    height: 0.0,
                }],
            }],
        };
        buf.extend_from_slice(&mv.pack()?);

        // 7. Survival (3 fields: EntityId, Life Gauge, Gauges map)
        let mut gauges = BTreeMap::new();
        gauges.insert("groggy".into(), GaugeMsg::constant(self.now_secs, 0.0, 100.0));
        let surv = SurvivalMsg {
            entity_id: self.entity_id.clone(),
            life: GaugeMsg::constant(self.now_secs, self.hp.max(0.0), self.max_hp.max(1.0)),
            gauges,
        };
        buf.extend_from_slice(&surv.pack()?);

        // 8. Role?, 9. EnemyId?
        write_mp_opt_str(&mut buf, &self.role)?;
        write_mp_opt_str(&mut buf, &self.enemy_id)?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let entity_type = read_mp_u16(&mut cur)?;
        let is_alive = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;

        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _disp_eid = read_mp_string(&mut cur)?;
        let base_scale = read_mp_f32(&mut cur)?;
        let _ = skip_mp_value(&mut cur)?; // CollectibleDisplay

        let level = read_mp_i32(&mut cur)?;

        // Move
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _mv_eid = read_mp_string(&mut cur)?;
        let mlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut motion_name = String::new();
        let mut motion_option = 0u8;
        let mut pos_x = 0.0f32;
        let mut pos_y = 0.0f32;
        let mut yaw = 0.0f32;
        let mut now_secs = 0.0f64;
        for _ in 0..mlen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            motion_name = read_mp_string(&mut cur)?;
            motion_option = read_mp_u8(&mut cur)?;
            let _ = read_mp_f32(&mut cur)?;
            let _ = read_mp_f32(&mut cur)?;
            let plen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            for _ in 0..plen {
                let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                pos_x = read_mp_f32(&mut cur)?;
                pos_y = read_mp_f32(&mut cur)?;
                yaw = read_mp_f32(&mut cur)?;
                now_secs = read_mp_f64(&mut cur)?;
                let _ = read_mp_u8(&mut cur)?;
                let _ = read_mp_f32(&mut cur)?;
            }
        }

        // Survival
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _s_eid = read_mp_string(&mut cur)?;
        let life = GaugeMsg::read_from(&mut cur)?;
        let _ = skip_mp_value(&mut cur)?; // Gauges map

        let role = read_mp_opt_string(&mut cur)?;
        let enemy_id = read_mp_opt_string(&mut cur)?;
        let hp = life.nodes.first().map_or(0.0, |&(_, v)| v);
        Ok(Self {
            entity_id,
            entity_type,
            is_alive,
            base_scale,
            level,
            motion_name,
            motion_option,
            pos_x,
            pos_y,
            yaw,
            hp,
            max_hp: life.max,
            role,
            enemy_id,
            now_secs,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ActionStatusMsg {
    pub id: String,
    pub stamina: i32,
    pub cooltime: f32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ActionsMsg {
    pub battle_actions: Vec<ActionStatusMsg>,
}

impl ActionsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.battle_actions.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for a in &self.battle_actions {
            write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, &a.id).map_err(|e| WireError::Encode(e.to_string()))?;
            write_i32(&mut buf, a.stamina).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f32(&mut buf, a.cooltime).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut battle_actions = Vec::with_capacity(safe_cap(len as usize, &cur));
        for _ in 0..len {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            battle_actions.push(ActionStatusMsg {
                id: read_mp_string(&mut cur)?,
                stamina: read_mp_i32(&mut cur)?,
                cooltime: read_mp_f32(&mut cur)?,
            });
        }
        Ok(Self { battle_actions })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TargetEntityOptTileMsg {
    pub entity_id: String,
    pub tile: Option<(u16, u16)>,
}

impl TargetEntityOptTileMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        match self.tile {
            Some((x, y)) => {
                write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
                write_u16(&mut buf, x).map_err(|e| WireError::Encode(e.to_string()))?;
                write_u16(&mut buf, y).map_err(|e| WireError::Encode(e.to_string()))?;
            }
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let tile = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            Some((read_mp_u16(&mut cur)?, read_mp_u16(&mut cur)?))
        };
        Ok(Self { entity_id, tile })
    }
}

pub type SelectBattleTargetMsg = TargetEntityOptTileMsg;
pub type EnterBattleMsg = TargetEntityOptTileMsg;

#[derive(Debug, Clone, PartialEq)]
pub struct BattleBegunMsg {
    pub entity_id: String,
    pub event_at: f64,
    pub enemy_id: String,
    pub start_damaged: bool,
}

impl BattleBegunMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.event_at).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.enemy_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.start_damaged).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
            event_at: read_mp_f64(&mut cur)?,
            enemy_id: read_mp_string(&mut cur)?,
            start_damaged: read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct EntityTimeMsg {
    pub entity_id: String,
    pub time: f64,
}

impl EntityTimeMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(40);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.time).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
            time: read_mp_f64(&mut cur)?,
        })
    }
}

pub type BattleEndedMsg = EntityTimeMsg;
pub type EntityDiedMsg = EntityTimeMsg;

#[derive(Debug, Clone, PartialEq)]
pub struct UseBattleActionMsg {
    pub action_id: String,
    pub start_at: f64,
    pub target_entity_id: Option<String>,
    pub target_tile: Option<(u16, u16)>,
    pub move_cancel_mode: Option<i32>,
}

impl UseBattleActionMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        let count = if self.move_cancel_mode.is_some() { 5 } else { 4 };
        write_array_len(&mut buf, count).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.action_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.start_at).map_err(|e| WireError::Encode(e.to_string()))?;
        write_mp_opt_str(&mut buf, &self.target_entity_id)?;
        match self.target_tile {
            Some((x, y)) => {
                write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
                write_u16(&mut buf, x).map_err(|e| WireError::Encode(e.to_string()))?;
                write_u16(&mut buf, y).map_err(|e| WireError::Encode(e.to_string()))?;
            }
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        if let Some(m) = self.move_cancel_mode {
            write_i32(&mut buf, m).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let action_id = read_mp_string(&mut cur)?;
        let start_at = read_mp_f64(&mut cur)?;
        let target_entity_id = read_mp_opt_string(&mut cur)?;
        let target_tile = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            Some((read_mp_u16(&mut cur)?, read_mp_u16(&mut cur)?))
        };
        let move_cancel_mode = if len >= 5 && !peek_is_nil(&cur) {
            Some(read_mp_i32(&mut cur)?)
        } else {
            None
        };
        Ok(Self {
            action_id,
            start_at,
            target_entity_id,
            target_tile,
            move_cancel_mode,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct DamageMsg {
    pub result: i32,
    pub value: i32,
    pub part: i32,
    pub direction: i32,
    pub attack_type: i32,
    pub effects: i32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct DamagedMsg {
    pub victim_id: String,
    pub attacker_id: String,
    pub damage: DamageMsg,
    pub event_at: f64,
}

impl DamagedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.victim_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.attacker_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 6).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.damage.result).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.damage.value).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.damage.part).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.damage.direction).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.damage.attack_type)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.damage.effects).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.event_at).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let victim_id = read_mp_string(&mut cur)?;
        let attacker_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let damage = DamageMsg {
            result: read_mp_i32(&mut cur)?,
            value: read_mp_i32(&mut cur)?,
            part: read_mp_i32(&mut cur)?,
            direction: read_mp_i32(&mut cur)?,
            attack_type: read_mp_i32(&mut cur)?,
            effects: read_mp_i32(&mut cur)?,
        };
        let event_at = read_mp_f64(&mut cur)?;
        Ok(Self {
            victim_id,
            attacker_id,
            damage,
            event_at,
        })
    }
}

// ---------------------------------------------------------------------------
// Step 8 Messages: Unity UI AddOnReady Bootstrap & Query Responses
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct StatisticsMsg {
    pub level: i32,
    pub exp: i32,
}

impl StatisticsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(24);
        // Statistics: 8 fields (BasicAbilities{}, DerivedsAbilities{}, Level, Exp,
        // ResistanceLevels{}, ResistanceExps{}, Modifiers{}, RepresentPowers{})
        write_array_len(&mut buf, 8).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.level.max(1)).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.exp.max(0)).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        skip_mp_value(&mut cur)?;
        skip_mp_value(&mut cur)?;
        let level = read_mp_i32(&mut cur)?;
        let exp = read_mp_i32(&mut cur)?;
        for _ in 0..4 {
            skip_mp_value(&mut cur)?;
        }
        Ok(Self { level, exp })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SkillBundleMsg {
    pub category: i32,
    pub skill_id: String,
    pub levels: BTreeMap<String, i32>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct SkillCategoryMsg {
    pub level: i32,
    pub exp: i32,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SkillsMsg {
    pub skill_list: Vec<SkillBundleMsg>,
    pub skill_point: i32,
    pub categories: BTreeMap<i32, SkillCategoryMsg>,
    pub untrained_count: i32,
}

impl SkillsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64 + self.skill_list.len() * 32 + self.categories.len() * 12);
        // Skills: 6 fields (SkillList[], SkillPoint, Categories{}, UntrainedCount, AdvisedSkills[], AdvisedSkillCategories{})
        write_array_len(&mut buf, 6)?;
        write_array_len(&mut buf, self.skill_list.len() as u32)?;
        for sb in &self.skill_list {
            write_array_len(&mut buf, 3)?;
            write_i32(&mut buf, sb.category)?;
            write_str(&mut buf, &sb.skill_id)?;
            write_map_len(&mut buf, sb.levels.len() as u32)?;
            for (sub, &lv) in &sb.levels {
                write_str(&mut buf, sub)?;
                write_i32(&mut buf, lv)?;
            }
        }
        write_i32(&mut buf, self.skill_point)?;
        write_map_len(&mut buf, self.categories.len() as u32)?;
        for (&cat, sc) in &self.categories {
            write_i32(&mut buf, cat)?;
            write_array_len(&mut buf, 4)?;
            write_i32(&mut buf, sc.level)?;
            write_i32(&mut buf, sc.exp)?;
            write_nil(&mut buf)?; // ResearchTime
            write_nil(&mut buf)?; // Researching
        }
        write_i32(&mut buf, self.untrained_count)?;
        write_array_len(&mut buf, 0)?; // AdvisedSkills
        write_map_len(&mut buf, 0)?; // AdvisedSkillCategories
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let slen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut skill_list = Vec::with_capacity(safe_cap(slen as usize, &cur));
        for _ in 0..slen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let category = read_mp_i32(&mut cur)?;
            let skill_id = read_mp_string(&mut cur)?;
            let mlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut levels = BTreeMap::new();
            for _ in 0..mlen {
                let k = read_mp_string(&mut cur)?;
                let v = read_mp_i32(&mut cur)?;
                levels.insert(k, v);
            }
            skill_list.push(SkillBundleMsg {
                category,
                skill_id,
                levels,
            });
        }
        let skill_point = read_mp_i32(&mut cur)?;
        let clen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut categories = BTreeMap::new();
        for _ in 0..clen {
            let cat = read_mp_i32(&mut cur)?;
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let level = read_mp_i32(&mut cur)?;
            let exp = read_mp_i32(&mut cur)?;
            skip_mp_value(&mut cur)?;
            skip_mp_value(&mut cur)?;
            categories.insert(cat, SkillCategoryMsg { level, exp });
        }
        let untrained_count = read_mp_i32(&mut cur)?;
        skip_mp_value(&mut cur)?;
        skip_mp_value(&mut cur)?;
        Ok(Self {
            skill_list,
            skill_point,
            categories,
            untrained_count,
        })
    }
}

/// LearnSkill (2048) — C->S `[SkillId: str, Level: i32, SubId: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LearnSkillMsg {
    pub skill_id: String,
    pub level: i32,
    pub sub_id: String,
}

impl LearnSkillMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.skill_id)?;
        write_i32(&mut out, self.level)?;
        write_str(&mut out, &self.sub_id)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let skill_id = read_mp_string(&mut cur)?;
        let level = read_mp_i32(&mut cur)?;
        let sub_id = read_mp_string(&mut cur)?;
        Ok(Self {
            skill_id,
            level,
            sub_id,
        })
    }
}

/// UntrainSkill (2049) — C->S `[SkillId: str, Level: i32, SubId: str, VoucherId: nil|str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct UntrainSkillMsg {
    pub skill_id: String,
    pub level: i32,
    pub sub_id: String,
    pub voucher_id: Option<String>,
}

impl UntrainSkillMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 4)?;
        write_str(&mut out, &self.skill_id)?;
        write_i32(&mut out, self.level)?;
        write_str(&mut out, &self.sub_id)?;
        write_mp_opt_str(&mut out, &self.voucher_id)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let skill_id = read_mp_string(&mut cur)?;
        let level = read_mp_i32(&mut cur)?;
        let sub_id = read_mp_string(&mut cur)?;
        let voucher_id = read_mp_opt_string(&mut cur)?;
        Ok(Self {
            skill_id,
            level,
            sub_id,
            voucher_id,
        })
    }
}

/// ResearchSkillCategory (2446) — C->S `[Category: i32, SkipCategory: nil|i32]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ResearchSkillCategoryMsg {
    pub category: i32,
    pub skip_category: Option<i32>,
}

impl ResearchSkillCategoryMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.category)?;
        match self.skip_category {
            None => write_nil(&mut out)?,
            Some(c) => write_i32(&mut out, c)?,
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let category = read_mp_i32(&mut cur)?;
        let skip_category = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_i32(&mut cur)?)
        };
        Ok(Self {
            category,
            skip_category,
        })
    }
}

/// ExpGained (3707) — S->C `[EntityId: str, Exp: i32, BonusExp: i32, ResistanceType: nil, ResistanceExp: i32]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ExpGainedMsg {
    pub entity_id: String,
    pub exp: i32,
    pub bonus_exp: i32,
    pub resistance_exp: i32,
}

impl ExpGainedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 5)?;
        write_str(&mut out, &self.entity_id)?;
        write_i32(&mut out, self.exp)?;
        write_i32(&mut out, self.bonus_exp)?;
        write_nil(&mut out)?;
        write_i32(&mut out, self.resistance_exp)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let exp = read_mp_i32(&mut cur)?;
        let bonus_exp = read_mp_i32(&mut cur)?;
        skip_mp_value(&mut cur)?;
        let resistance_exp = read_mp_i32(&mut cur)?;
        Ok(Self {
            entity_id,
            exp,
            bonus_exp,
            resistance_exp,
        })
    }
}

/// Rewarded (2065) carrying a character LevelUpEffect (2062, hint=true) + empty RewardInfo(16 fields)
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct RewardedLevelUpMsg {
    pub level: i32,
}

impl RewardedLevelUpMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(48);
        write_array_len(&mut out, 2)?;
        // LevelUpEffect with hint=true -> [2062u32, RewardEffect::LevelUp(1), level]
        write_array_len(&mut out, 3)?;
        write_u32(&mut out, TC_LEVEL_UP_EFFECT)?;
        write_i32(&mut out, REWARD_EFFECT_LEVEL_UP)?;
        write_i32(&mut out, self.level)?;
        // RewardInfo: 16 fields (nulls / empty maps & arrays)
        write_array_len(&mut out, 16)?;
        write_nil(&mut out)?; // Exp
        write_map_len(&mut out, 0)?; // Currency
        write_nil(&mut out)?; // SkillPoints
        write_nil(&mut out)?; // UsableSkillPoints
        write_map_len(&mut out, 0)?; // Abilities
        write_map_len(&mut out, 0)?; // DerivedAbilities
        write_array_len(&mut out, 0)?; // UnlockedSkills
        write_array_len(&mut out, 0)?; // Titles
        write_map_len(&mut out, 0)?; // FriendshipPoint
        write_array_len(&mut out, 0)?; // Items
        write_array_len(&mut out, 0)?; // RandomItems
        write_array_len(&mut out, 0)?; // Vouchers
        write_nil(&mut out)?; // QuestScore
        write_array_len(&mut out, 0)?; // RecipeIds
        write_array_len(&mut out, 0)?; // BlueprintIds
        write_array_len(&mut out, 0)?; // Memos
        Ok(out)
    }
}

/// GetQuests (237918) — C->S `[Category: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GetQuestsMsg {
    pub category: String,
}

impl GetQuestsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 1)?;
        write_str(&mut out, &self.category)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            category: read_mp_string(&mut cur)?,
        })
    }
}

/// Quests (237919) — S->C `[Category: str, Todos: []]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct QuestsMsg {
    pub category: String,
}

impl QuestsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.category)?;
        write_array_len(&mut out, 0)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let category = read_mp_string(&mut cur)?;
        skip_mp_value(&mut cur)?;
        Ok(Self { category })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct PetsInfoMsg {
    pub grazable_count: i32,
}

impl PetsInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(16);
        // PetsInfo: 3 fields (Pets([[]]), GrazedPets([[]]), GrazableCount)
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.grazable_count).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        skip_mp_value(&mut cur)?;
        skip_mp_value(&mut cur)?;
        Ok(Self {
            grazable_count: read_mp_i32(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AttachableAccessoriesMsg {
    pub accessories: Vec<String>,
}

impl AttachableAccessoriesMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(16);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.accessories.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for a in &self.accessories {
            write_str(&mut buf, a).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut accessories = Vec::with_capacity(safe_cap(len as usize, &cur));
        for _ in 0..len {
            accessories.push(read_mp_string(&mut cur)?);
        }
        Ok(Self { accessories })
    }
}

pub fn pack_empty_resistance_exp_caps() -> Result<Vec<u8>, WireError> {
    let mut buf = Vec::with_capacity(4);
    write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
    write_map_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
    Ok(buf)
}

pub fn pack_empty_estate_licenses() -> Result<Vec<u8>, WireError> {
    let mut buf = Vec::with_capacity(24);
    // EstateLicenses: 10 fields
    write_array_len(&mut buf, 10).map_err(|e| WireError::Encode(e.to_string()))?;
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // UrbanEstate
    write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // LargestUrbanEstateSize
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // PersonalEstate
    write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // LargestPersonalEstateSize
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // ClanEstate
    write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // LargestClanEstateSize
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // ClanCargoWarphole
    write_f64(&mut buf, 0.0).map_err(|e| WireError::Encode(e.to_string()))?; // VisitAvailableAt
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // PersonalWarpholeTile
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // UrbanWarpholeTile
    Ok(buf)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AvailableEmotionsMsg {
    pub motions: Vec<String>,
    pub emoticons: Vec<String>,
}

impl AvailableEmotionsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.motions.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for m in &self.motions {
            write_str(&mut buf, m).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_array_len(&mut buf, self.emoticons.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for e in &self.emoticons {
            write_str(&mut buf, e).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut motions = Vec::with_capacity(safe_cap(mlen as usize, &cur));
        for _ in 0..mlen {
            motions.push(read_mp_string(&mut cur)?);
        }
        let elen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut emoticons = Vec::with_capacity(safe_cap(elen as usize, &cur));
        for _ in 0..elen {
            emoticons.push(read_mp_string(&mut cur)?);
        }
        Ok(Self { motions, emoticons })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CheatFlagsMsg {
    pub flags: BTreeMap<String, bool>,
}

impl CheatFlagsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(16);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(&mut buf, self.flags.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.flags {
            write_str(&mut buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_bool(&mut buf, *v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let len = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut flags = BTreeMap::new();
        for _ in 0..len {
            let k = read_mp_string(&mut cur)?;
            let v = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            flags.insert(k, v);
        }
        Ok(Self { flags })
    }
}

pub fn pack_empty_quest_categories() -> Result<Vec<u8>, WireError> {
    let mut buf = Vec::with_capacity(4);
    write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
    write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
    write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;
    Ok(buf)
}

// ---------------------------------------------------------------------------
// Step 9 Messages: Island Map/Warp & Player Market
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GetRegionMapInfoMsg {
    pub region_id: String,
}

impl GetRegionMapInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.region_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            region_id: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct RegionMapInfoMsg {
    pub region_id: String,
    pub terrain_id: String,
    pub tile_count_x: u16,
    pub tile_count_y: u16,
    pub defogged_chunks: Vec<(u8, u8)>,
}

impl RegionMapInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64 + self.defogged_chunks.len() * 4);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.region_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.terrain_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_count_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_count_y).map_err(|e| WireError::Encode(e.to_string()))?;
        let defog = DefoggedChunks {
            chunks: self.defogged_chunks.clone(),
        };
        buf.extend_from_slice(&defog.pack()?);
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let region_id = read_mp_string(&mut cur)?;
        let terrain_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_count_x = read_mp_u16(&mut cur)?;
        let tile_count_y = read_mp_u16(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let clen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut defogged_chunks = Vec::with_capacity(safe_cap(clen as usize, &cur));
        for _ in 0..clen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            defogged_chunks.push((read_mp_u8(&mut cur)?, read_mp_u8(&mut cur)?));
        }
        Ok(Self {
            region_id,
            terrain_id,
            tile_count_x,
            tile_count_y,
            defogged_chunks,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct WarpMsg {
    pub tile_x: u16,
    pub tile_y: u16,
}

impl WarpMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(8);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            tile_x: read_mp_u16(&mut cur)?,
            tile_y: read_mp_u16(&mut cur)?,
        })
    }
}

pub fn pack_empty_warp_costs() -> Result<Vec<u8>, WireError> {
    let mut buf = Vec::with_capacity(4);
    write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
    write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
    Ok(buf)
}

#[derive(Debug, Clone, PartialEq)]
pub struct RegisterProductMsg {
    pub item_id: String,
    pub price: i64,
    pub duration: f32,
}

impl RegisterProductMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.item_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i64(&mut buf, self.price).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(&mut buf, self.duration).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let item_id = read_mp_string(&mut cur)?;
        let price: i64 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let duration = read_mp_f32(&mut cur)?;
        Ok(Self {
            item_id,
            price,
            duration,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BuyProductMsg {
    pub product_id: String,
    pub payment_ids: Vec<String>,
}

impl BuyProductMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.product_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.payment_ids.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for id in &self.payment_ids {
            write_str(&mut buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let product_id = read_mp_string(&mut cur)?;
        let plen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut payment_ids = Vec::with_capacity(safe_cap(plen as usize, &cur));
        for _ in 0..plen {
            payment_ids.push(read_mp_string(&mut cur)?);
        }
        Ok(Self {
            product_id,
            payment_ids,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SearchProductsMsg {
    pub item_name: Option<String>,
    pub prototype_id: Option<String>,
    pub skip: i32,
}

impl SearchProductsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(48);
        // SearchProducts: 9 fields
        write_array_len(&mut buf, 9).map_err(|e| WireError::Encode(e.to_string()))?;
        write_mp_opt_str(&mut buf, &self.item_name)?;
        write_mp_opt_str(&mut buf, &self.prototype_id)?;
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // NestedTags
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Category
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // SubCategories
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Price
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Level
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?; // SortCondition
        write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, false).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.skip).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let item_name = read_mp_opt_string(&mut cur)?;
        let prototype_id = read_mp_opt_string(&mut cur)?;
        for _ in 0..6 {
            skip_mp_value(&mut cur)?;
        }
        let skip = read_mp_i32(&mut cur)?;
        Ok(Self {
            item_name,
            prototype_id,
            skip,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ProductMsg {
    pub id: String,
    pub region_id: String,
    pub listed_at: f64,
    pub expires_at: f64,
    pub deletes_at: f64,
    pub price: i64,
    pub items: Vec<ItemMsg>,
    pub level: i32,
    pub durability: f32,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ProductsMsg {
    pub products: Vec<ProductMsg>,
}

impl ProductsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64 + self.products.len() * 192);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.products.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for p in &self.products {
            // Product: 13 fields
            write_array_len(&mut buf, 13).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, &p.id).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, &p.region_id).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f64(&mut buf, p.listed_at).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f64(&mut buf, p.expires_at).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f64(&mut buf, p.deletes_at).map_err(|e| WireError::Encode(e.to_string()))?;
            write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // PurchasedAt
            write_i64(&mut buf, p.price).map_err(|e| WireError::Encode(e.to_string()))?;
            write_i64(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // Fee
            write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // Currency (TStone=0)
            write_array_len(&mut buf, p.items.len() as u32)
                .map_err(|e| WireError::Encode(e.to_string()))?;
            for it in &p.items {
                it.write_to(&mut buf)?;
            }
            write_i32(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // State (Listed=0)
            write_i32(&mut buf, p.level).map_err(|e| WireError::Encode(e.to_string()))?;
            write_f32(&mut buf, p.durability).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let plen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut products = Vec::with_capacity(safe_cap(plen as usize, &cur));
        for _ in 0..plen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let id = read_mp_string(&mut cur)?;
            let region_id = read_mp_string(&mut cur)?;
            let listed_at = read_mp_f64(&mut cur)?;
            let expires_at = read_mp_f64(&mut cur)?;
            let deletes_at = read_mp_f64(&mut cur)?;
            skip_mp_value(&mut cur)?; // PurchasedAt
            let price: i64 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let _fee: i64 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let _currency = read_mp_i32(&mut cur)?;
            let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut items = Vec::with_capacity(safe_cap(ilen as usize, &cur));
            for _ in 0..ilen {
                items.push(ItemMsg::read_from(&mut cur)?);
            }
            let _state = read_mp_i32(&mut cur)?;
            let level = read_mp_i32(&mut cur)?;
            let durability = read_mp_f32(&mut cur)?;
            products.push(ProductMsg {
                id,
                region_id,
                listed_at,
                expires_at,
                deletes_at,
                price,
                items,
                level,
                durability,
            });
        }
        Ok(Self { products })
    }
}

// ---------------------------------------------------------------------------
// Step 10 Messages: Multi-Island Sea-Chart Travel & Clans
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct MemberMsg {
    pub entity_id: String,
    pub clan_id: String,
    pub clan_name: String,
    pub role_id: i32,
    pub applying_clan_id: Option<String>,
}

impl MemberMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.clan_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.clan_name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.role_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_mp_opt_str(&mut buf, &self.applying_clan_id)?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            entity_id: read_mp_string(&mut cur)?,
            clan_id: read_mp_string(&mut cur)?,
            clan_name: read_mp_string(&mut cur)?,
            role_id: read_mp_i32(&mut cur)?,
            applying_clan_id: read_mp_opt_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct MakeClanMsg {
    pub clan_name: String,
    pub currency: i32,
}

impl MakeClanMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.clan_name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.currency).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            clan_name: read_mp_string(&mut cur)?,
            currency: read_mp_i32(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LeaveClanMsg {
    pub clan_id: String,
}

impl LeaveClanMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.clan_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            clan_id: read_mp_string(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct EmigratedMsg {
    pub teleport_type: i32,
}

impl EmigratedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(8);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.teleport_type).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            teleport_type: read_mp_i32(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RecommendedStableRegionsMsg {
    pub region_ids: Vec<String>,
}

impl RecommendedStableRegionsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32 + self.region_ids.len() * 16);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.region_ids.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for rid in &self.region_ids {
            // Route: 2 fields (RegionId, Price?=nil -> free)
            write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
            write_str(&mut buf, rid).map_err(|e| WireError::Encode(e.to_string()))?;
            write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let rlen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut region_ids = Vec::with_capacity(safe_cap(rlen as usize, &cur));
        for _ in 0..rlen {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            region_ids.push(read_mp_string(&mut cur)?);
            skip_mp_value(&mut cur)?; // Price?
        }
        Ok(Self { region_ids })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TravelToStableRegionMsg {
    pub region_id: String,
}

impl TravelToStableRegionMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(24);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.region_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            region_id: read_mp_string(&mut cur)?,
        })
    }
}

pub fn pack_empty_routes_of_archipelago() -> Result<Vec<u8>, WireError> {
    let mut buf = Vec::with_capacity(4);
    write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
    write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?;
    Ok(buf)
}

#[derive(Debug, Clone, PartialEq)]
pub struct SurvivalMsg {
    pub entity_id: String,
    pub life: GaugeMsg,
    pub gauges: BTreeMap<String, GaugeMsg>,
}

impl SurvivalMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128);
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        self.life.write_to(&mut buf)?;
        write_map_len(&mut buf, self.gauges.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, g) in &self.gauges {
            write_str(&mut buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            g.write_to(&mut buf)?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let life = GaugeMsg::read_from(&mut cur)?;
        let mlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut gauges = BTreeMap::new();
        for _ in 0..mlen {
            let k = read_mp_string(&mut cur)?;
            let g = GaugeMsg::read_from(&mut cur)?;
            gauges.insert(k, g);
        }
        Ok(Self {
            entity_id,
            life,
            gauges,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TouchMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub entity_type: u16,
}

impl TouchMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 3).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.entity_type).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let entity_type = read_mp_u16(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            entity_type,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct GeneratorMsg {
    pub id: String,
    pub level: i32,
    pub name: String,
    pub icon: String,
    pub amount: i32,
    pub effort: f32,
    pub duration: f32,
    pub tool_requirements: BTreeMap<String, i32>,
    pub enabled: bool,
}

impl GeneratorMsg {
    fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 9).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, self.level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.icon).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(buf, self.amount).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(buf, self.effort).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(buf, self.duration).map_err(|e| WireError::Encode(e.to_string()))?;
        write_map_len(buf, self.tool_requirements.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for (k, v) in &self.tool_requirements {
            write_str(buf, k).map_err(|e| WireError::Encode(e.to_string()))?;
            write_i32(buf, *v).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_bool(buf, self.enabled).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(())
    }

    fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let id = read_mp_string(cur)?;
        let level = read_mp_i32(cur)?;
        let name = read_mp_string(cur)?;
        let icon = read_mp_string(cur)?;
        let amount = read_mp_i32(cur)?;
        let effort = read_mp_f32(cur)?;
        let duration = read_mp_f32(cur)?;
        let mlen = read_map_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut tool_requirements = BTreeMap::new();
        for _ in 0..mlen {
            let k = read_mp_string(cur)?;
            let v = read_mp_i32(cur)?;
            tool_requirements.insert(k, v);
        }
        let enabled = read_bool(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            id,
            level,
            name,
            icon,
            amount,
            effort,
            duration,
            tool_requirements,
            enabled,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct CollectibleMsg {
    pub entity_id: String,
    pub collectible_id: Option<String>,
    pub size: String,
    pub generators: Vec<GeneratorMsg>,
    pub critical_generator: String,
}

impl CollectibleMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(128);
        self.write_to(&mut buf)?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        Self::read_from(&mut cur)
    }

    fn write_to(&self, buf: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_mp_opt_str(buf, &self.collectible_id)?;
        write_str(buf, &self.size).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(buf, self.generators.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for g in &self.generators {
            g.write_to(buf)?;
        }
        write_str(buf, &self.critical_generator).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(())
    }

    fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(cur)?;
        let collectible_id = read_mp_opt_string(cur)?;
        let size = read_mp_string(cur)?;
        let glen = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut generators = Vec::with_capacity(safe_cap(glen as usize, cur));
        for _ in 0..glen {
            generators.push(GeneratorMsg::read_from(cur)?);
        }
        let critical_generator = read_mp_string(cur)?;
        Ok(Self {
            entity_id,
            collectible_id,
            size,
            generators,
            critical_generator,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct TouchedMsg {
    pub entity_id: String,
    pub entity_name: String,
    pub prototype_id: String,
    pub level: i32,
    pub interactions: Vec<i32>,
    pub collectible: CollectibleMsg,
}

impl TouchedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(192);
        // Touched: 12 fields
        write_array_len(&mut buf, 12).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_name).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.prototype_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.interactions.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for &i in &self.interactions {
            write_i32(&mut buf, i).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // DisabledInteractions
        write_array_len(&mut buf, 0).map_err(|e| WireError::Encode(e.to_string()))?; // AccessDeniedInteractions
        self.collectible.write_to(&mut buf)?;
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Workbench
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Secured
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // ReactingProp
        write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?; // Mannequin
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let entity_name = read_mp_string(&mut cur)?;
        let prototype_id = read_mp_string(&mut cur)?;
        let level = read_mp_i32(&mut cur)?;
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut interactions = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            interactions.push(read_mp_i32(&mut cur)?);
        }
        let _ = skip_mp_value(&mut cur)?; // DisabledInteractions
        let _ = skip_mp_value(&mut cur)?; // AccessDeniedInteractions
        let collectible = CollectibleMsg::read_from(&mut cur)?;
        for _ in 0..4 {
            let _ = skip_mp_value(&mut cur)?;
        }
        Ok(Self {
            entity_id,
            entity_name,
            prototype_id,
            level,
            interactions,
            collectible,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CollectMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub generator_id: String,
    pub level: i32,
    pub tool_item_id: String,
}

impl CollectMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64);
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.generator_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.tool_item_id).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let generator_id = read_mp_string(&mut cur)?;
        let level = read_mp_i32(&mut cur)?;
        let tool_item_id = read_mp_string(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            generator_id,
            level,
            tool_item_id,
        })
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct TimerMsg {
    pub duration: f32,
}

impl TimerMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(8);
        write_array_len(&mut buf, 1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(&mut buf, self.duration).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            duration: read_mp_f32(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct CollectedMsg {
    pub items: Vec<ItemMsg>,
    pub result: i32,
    pub action_level: i32,
    pub potential_level: i32,
    pub success_ratio: f32,
    pub ran_out: bool,
}

impl CollectedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(64 + self.items.len() * 128);
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.items.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for item in &self.items {
            item.write_to(&mut buf)?;
        }
        write_i32(&mut buf, self.result).map_err(|e| WireError::Encode(e.to_string()))?;
        // ActionInfo (5 fields: ActionLevel, PotentialLevel, RelatedCategory=-1, SuccessRatio, RelatedAbility=-1)
        write_array_len(&mut buf, 5).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.action_level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, self.potential_level).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, -1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f32(&mut buf, self.success_ratio).map_err(|e| WireError::Encode(e.to_string()))?;
        write_i32(&mut buf, -1).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.ran_out).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut items = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            items.push(ItemMsg::read_from(&mut cur)?);
        }
        let result = read_mp_i32(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let action_level = read_mp_i32(&mut cur)?;
        let potential_level = read_mp_i32(&mut cur)?;
        let _related_category = read_mp_i32(&mut cur)?;
        let success_ratio = read_mp_f32(&mut cur)?;
        let _related_ability = read_mp_i32(&mut cur)?;
        let ran_out = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self {
            items,
            result,
            action_level,
            potential_level,
            success_ratio,
            ran_out,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DisappearEntityOnTileMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
}

impl DisappearEntityOnTileMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_x).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u16(&mut buf, self.tile_y).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LockOrUnlockItemsMsg {
    pub lock: bool,
    pub item_ids: Vec<String>,
}

impl LockOrUnlockItemsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(32);
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_bool(&mut buf, self.lock).map_err(|e| WireError::Encode(e.to_string()))?;
        write_array_len(&mut buf, self.item_ids.len() as u32)
            .map_err(|e| WireError::Encode(e.to_string()))?;
        for id in &self.item_ids {
            write_str(&mut buf, id).map_err(|e| WireError::Encode(e.to_string()))?;
        }
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let lock = read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let ilen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut item_ids = Vec::with_capacity(safe_cap(ilen as usize, &cur));
        for _ in 0..ilen {
            item_ids.push(read_mp_string(&mut cur)?);
        }
        Ok(Self { lock, item_ids })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct SayInExclusiveChannelMsg {
    pub entity_id: String,
    pub time: f64,
    pub text: String,
    pub speaker_name: Option<String>,
    pub speaker_freq: i32,
    pub channel_type: i32,
}

impl SayInExclusiveChannelMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut buf = Vec::with_capacity(96);
        // SayInExclusiveChannel: 2 fields (Message_, ChannelType)
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        // Message_: 4 fields (EntityId, Time, Body(RadioTalk hint=true), Speaker(RadioId?))
        write_array_len(&mut buf, 4).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.entity_id).map_err(|e| WireError::Encode(e.to_string()))?;
        write_f64(&mut buf, self.time).map_err(|e| WireError::Encode(e.to_string()))?;
        // Body: RadioTalk with hint = true -> [2601, text]
        write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
        write_u32(&mut buf, TC_RADIO_TALK).map_err(|e| WireError::Encode(e.to_string()))?;
        write_str(&mut buf, &self.text).map_err(|e| WireError::Encode(e.to_string()))?;
        // Speaker: RadioId? -> nil or [name, freq]
        match &self.speaker_name {
            Some(name) => {
                write_array_len(&mut buf, 2).map_err(|e| WireError::Encode(e.to_string()))?;
                write_str(&mut buf, name).map_err(|e| WireError::Encode(e.to_string()))?;
                write_i32(&mut buf, self.speaker_freq)
                    .map_err(|e| WireError::Encode(e.to_string()))?;
            }
            None => write_nil(&mut buf).map_err(|e| WireError::Encode(e.to_string()))?,
        }
        write_i32(&mut buf, self.channel_type).map_err(|e| WireError::Encode(e.to_string()))?;
        Ok(buf)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let time = read_mp_f64(&mut cur)?;
        let text = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            String::new()
        } else {
            let blen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let mut extracted = String::new();
            if blen >= 2 {
                let tc: u32 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                if tc == TC_RADIO_TALK {
                    extracted = read_mp_string(&mut cur)?;
                    for _ in 2..blen {
                        skip_mp_value(&mut cur)?;
                    }
                } else {
                    for _ in 1..blen {
                        skip_mp_value(&mut cur)?;
                    }
                }
            } else {
                for _ in 0..blen {
                    skip_mp_value(&mut cur)?;
                }
            }
            extracted
        };
        let (speaker_name, speaker_freq) = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            (None, 0)
        } else {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let name = read_mp_string(&mut cur)?;
            let freq = read_mp_i32(&mut cur)?;
            (Some(name), freq)
        };
        let channel_type = read_mp_i32(&mut cur)?;
        Ok(Self {
            entity_id,
            time,
            text,
            speaker_name,
            speaker_freq,
            channel_type,
        })
    }
}

/// Revive (2101) — C->S `[nil | [u16, u16]]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReviveMsg {
    pub warphole_tile: Option<(u16, u16)>,
}

impl ReviveMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 1)?;
        match self.warphole_tile {
            None => write_nil(&mut out)?,
            Some((x, y)) => {
                write_array_len(&mut out, 2)?;
                write_u16(&mut out, x)?;
                write_u16(&mut out, y)?;
            }
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        if len < 1 {
            return Ok(Self {
                warphole_tile: None,
            });
        }
        let warphole_tile = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            let x = read_mp_u16(&mut cur)?;
            let y = read_mp_u16(&mut cur)?;
            Some((x, y))
        };
        Ok(Self { warphole_tile })
    }
}

/// ReviveImmediately (210201) — C->S `[nil | str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReviveImmediatelyMsg {
    pub voucher_id: Option<String>,
}

impl ReviveImmediatelyMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 1)?;
        match &self.voucher_id {
            None => write_nil(&mut out)?,
            Some(v) => write_str(&mut out, v)?,
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        if len < 1 {
            return Ok(Self { voucher_id: None });
        }
        let voucher_id = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_string(&mut cur)?)
        };
        Ok(Self { voucher_id })
    }
}

/// ReviveImmediatelyInfo (210102) — S->C `[UsedCount: i32, TotalCost: [Amount: i32, Currency: i32]]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReviveImmediatelyInfoMsg {
    pub used_count: i32,
    pub cost_amount: i32,
    pub cost_currency: i32,
}

impl ReviveImmediatelyInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.used_count)?;
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.cost_amount)?;
        write_i32(&mut out, self.cost_currency)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let used_count = read_mp_i32(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let cost_amount = read_mp_i32(&mut cur)?;
        let cost_currency = read_mp_i32(&mut cur)?;
        Ok(Self {
            used_count,
            cost_amount,
            cost_currency,
        })
    }
}

/// EntityRevived (119119) — S->C `[EntityId: str, At: f64]`
#[derive(Debug, Clone, PartialEq)]
pub struct EntityRevivedMsg {
    pub entity_id: String,
    pub at: f64,
}

impl EntityRevivedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.entity_id)?;
        write_f64(&mut out, self.at)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let at = read_mp_f64(&mut cur)?;
        Ok(Self { entity_id, at })
    }
}

/// Teleported (2037) — S->C `[Tile: [u16, u16], Type: i32]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TeleportedMsg {
    pub tile_x: u16,
    pub tile_y: u16,
    pub teleport_type: i32,
}

impl TeleportedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 2)?;
        write_array_len(&mut out, 2)?;
        write_u16(&mut out, self.tile_x)?;
        write_u16(&mut out, self.tile_y)?;
        write_i32(&mut out, self.teleport_type)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let teleport_type = read_mp_i32(&mut cur)?;
        Ok(Self {
            tile_x,
            tile_y,
            teleport_type,
        })
    }
}

/// GetCollectible (2017) — C->S `[EntityId: str, Tile: [u16, u16]]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GetCollectibleMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
}

impl GetCollectibleMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.entity_id)?;
        write_array_len(&mut out, 2)?;
        write_u16(&mut out, self.tile_x)?;
        write_u16(&mut out, self.tile_y)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
        })
    }
}

/// CollectibleChanged (2417) — S->C `[EntityId: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CollectibleChangedMsg {
    pub entity_id: String,
}

impl CollectibleChangedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::new();
        write_array_len(&mut out, 1)?;
        write_str(&mut out, &self.entity_id)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        Ok(Self { entity_id })
    }
}

/// RunUp (7300013) — C->S `[Kind: u8, On: i32]` (0=hello/sync, 1=set sprint state)
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct RunUpMsg {
    pub kind: u8,
    pub on: i32,
}

impl RunUpMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(8);
        write_array_len(&mut out, 2)?;
        write_u8(&mut out, self.kind)?;
        write_i32(&mut out, self.on)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let kind = read_mp_u8(&mut cur)?;
        let on = read_mp_i32(&mut cur)?;
        Ok(Self { kind, on })
    }
}

/// RunDown (7300014) — S->C `[Kind: u8, Json: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RunDownMsg {
    pub kind: u8,
    pub json: String,
}

impl RunDownMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 2)?;
        write_u8(&mut out, self.kind)?;
        write_str(&mut out, &self.json)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let kind = read_mp_u8(&mut cur)?;
        let json = read_mp_string(&mut cur)?;
        Ok(Self { kind, json })
    }
}

/// SetBaseMoveSpeed (8749871) — S->C `[EntityId: str, NormalSpeed: i32, BattleSpeed: i32]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SetBaseMoveSpeedMsg {
    pub entity_id: String,
    pub normal_speed: i32,
    pub battle_speed: i32,
}

impl SetBaseMoveSpeedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(48);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.entity_id)?;
        write_i32(&mut out, self.normal_speed)?;
        write_i32(&mut out, self.battle_speed)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let normal_speed = read_mp_i32(&mut cur)?;
        let battle_speed = read_mp_i32(&mut cur)?;
        Ok(Self {
            entity_id,
            normal_speed,
            battle_speed,
        })
    }
}

/// EstimateCraft (7) — C->S `[RecipeId, Materials: map<str, [str]>, ToolItemId?, Workbench?, ReformSlotIndex?]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EstimateCraftMsg {
    pub recipe_id: String,
    pub materials: BTreeMap<String, Vec<String>>,
    pub tool_item_id: Option<String>,
}

impl EstimateCraftMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 5)?;
        write_str(&mut out, &self.recipe_id)?;
        write_map_len(&mut out, self.materials.len() as u32)?;
        for (slot, ids) in &self.materials {
            write_str(&mut out, slot)?;
            write_array_len(&mut out, ids.len() as u32)?;
            for id in ids {
                write_str(&mut out, id)?;
            }
        }
        write_mp_opt_str(&mut out, &self.tool_item_id)?;
        write_nil(&mut out)?; // Workbench
        write_nil(&mut out)?; // ReformSlotIndex
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let recipe_id = read_mp_string(&mut cur)?;
        let mut materials = BTreeMap::new();
        if len >= 2 {
            let map_len = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            for _ in 0..map_len {
                let slot = read_mp_string(&mut cur)?;
                let arr_len =
                    read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                let mut ids = Vec::with_capacity(safe_cap(arr_len as usize, &cur));
                for _ in 0..arr_len {
                    ids.push(read_mp_string(&mut cur)?);
                }
                materials.insert(slot, ids);
            }
        }
        let tool_item_id = if len >= 3 {
            read_mp_opt_string(&mut cur)?
        } else {
            None
        };
        for _ in 3..len {
            let _ = skip_mp_value(&mut cur)?;
        }
        Ok(Self {
            recipe_id,
            materials,
            tool_item_id,
        })
    }
}

/// CraftEstimation (8) — embedded 10-element array inside CraftEstimationInfo (406132)
#[derive(Debug, Clone, PartialEq)]
pub struct CraftEstimationMsg {
    pub prototype_id: String,
    pub level: i32,
    pub name: String,
    pub durability_min: f32,
    pub durability_max: f32,
    pub tags: BTreeMap<String, i32>,
    pub unrevealed_rare_tag_count: i32,
    pub modifiable_count: i32,
    pub success_rate: f32,
    pub great_success_rate: f32,
    pub required_ability_value: f32,
}

impl CraftEstimationMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 10)?;
        write_str(out, &self.prototype_id)?;
        write_i32(out, self.level)?;
        write_str(out, &self.name)?;
        write_array_len(out, 2)?;
        write_f32(out, self.durability_min)?;
        write_f32(out, self.durability_max)?;
        write_map_len(out, self.tags.len() as u32)?;
        for (k, v) in &self.tags {
            write_str(out, k)?;
            write_i32(out, *v)?;
        }
        write_i32(out, self.unrevealed_rare_tag_count)?;
        write_i32(out, self.modifiable_count)?;
        write_f32(out, self.success_rate)?;
        write_f32(out, self.great_success_rate)?;
        write_f32(out, self.required_ability_value)?;
        Ok(())
    }

    pub fn read_from(cur: &mut Cursor<&[u8]>) -> Result<Self, WireError> {
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let prototype_id = read_mp_string(cur)?;
        let level = read_mp_i32(cur)?;
        let name = read_mp_string(cur)?;
        let _ = read_array_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let durability_min = read_mp_f32(cur)?;
        let durability_max = read_mp_f32(cur)?;
        let map_len = read_map_len(cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut tags = BTreeMap::new();
        for _ in 0..map_len {
            let k = read_mp_string(cur)?;
            let v = read_mp_i32(cur)?;
            tags.insert(k, v);
        }
        let unrevealed_rare_tag_count = read_mp_i32(cur)?;
        let modifiable_count = read_mp_i32(cur)?;
        let success_rate = read_mp_f32(cur)?;
        let great_success_rate = read_mp_f32(cur)?;
        let required_ability_value = read_mp_f32(cur)?;
        Ok(Self {
            prototype_id,
            level,
            name,
            durability_min,
            durability_max,
            tags,
            unrevealed_rare_tag_count,
            modifiable_count,
            success_rate,
            great_success_rate,
            required_ability_value,
        })
    }
}

/// CraftEstimationInfo (406132) — S->C `[CraftLevel: i32, CraftEstimation?]`
#[derive(Debug, Clone, PartialEq)]
pub struct CraftEstimationInfoMsg {
    pub craft_level: i32,
    pub craft_estimation: Option<CraftEstimationMsg>,
}

impl CraftEstimationInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(96);
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.craft_level)?;
        match &self.craft_estimation {
            Some(est) => est.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let craft_level = read_mp_i32(&mut cur)?;
        let pos = cur.position() as usize;
        let craft_estimation = if bytes.get(pos) == Some(&0xc0) {
            cur.set_position((pos + 1) as u64);
            None
        } else {
            Some(CraftEstimationMsg::read_from(&mut cur)?)
        };
        Ok(Self {
            craft_level,
            craft_estimation,
        })
    }
}

// ---------------------------------------------------------------------------
// Phase 1 Parity Wire Messages: Recipes, Blueprints, Estates, Warp/Points, POIs, Routes
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct RecipesMsg {
    pub ids: Vec<String>,
    pub liked_recipe_ids: Vec<String>,
    pub new_recipe_ids: Vec<String>,
}

impl RecipesMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64 + self.ids.len() * 16);
        write_array_len(&mut out, 3)?;
        write_array_len(&mut out, self.ids.len() as u32)?;
        for id in &self.ids {
            write_str(&mut out, id)?;
        }
        write_array_len(&mut out, self.liked_recipe_ids.len() as u32)?;
        for id in &self.liked_recipe_ids {
            write_str(&mut out, id)?;
        }
        write_array_len(&mut out, self.new_recipe_ids.len() as u32)?;
        for id in &self.new_recipe_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let read_vec = |c: &mut Cursor<&[u8]>| -> Result<Vec<String>, WireError> {
            let n = read_array_len(c).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            let mut v = Vec::with_capacity(safe_cap(n, c));
            for _ in 0..n {
                v.push(read_mp_string(c)?);
            }
            Ok(v)
        };
        Ok(Self {
            ids: read_vec(&mut cur)?,
            liked_recipe_ids: read_vec(&mut cur)?,
            new_recipe_ids: read_vec(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ArtifactBlueprintsMsg {
    pub ids: Vec<String>,
    pub liked_blueprint_ids: Vec<String>,
    pub new_blueprint_ids: Vec<String>,
}

impl ArtifactBlueprintsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64 + self.ids.len() * 16);
        write_array_len(&mut out, 3)?;
        write_array_len(&mut out, self.ids.len() as u32)?;
        for id in &self.ids {
            write_str(&mut out, id)?;
        }
        write_array_len(&mut out, self.liked_blueprint_ids.len() as u32)?;
        for id in &self.liked_blueprint_ids {
            write_str(&mut out, id)?;
        }
        write_array_len(&mut out, self.new_blueprint_ids.len() as u32)?;
        for id in &self.new_blueprint_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let read_vec = |c: &mut Cursor<&[u8]>| -> Result<Vec<String>, WireError> {
            let n = read_array_len(c).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            let mut v = Vec::with_capacity(safe_cap(n, c));
            for _ in 0..n {
                v.push(read_mp_string(c)?);
            }
            Ok(v)
        };
        Ok(Self {
            ids: read_vec(&mut cur)?,
            liked_blueprint_ids: read_vec(&mut cur)?,
            new_blueprint_ids: read_vec(&mut cur)?,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EstimateBuildMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub tool_id: String,
    pub materials: BTreeMap<String, Vec<String>>,
}

impl EstimateBuildMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let tool_id = read_mp_string(&mut cur)?;
        let mut materials = BTreeMap::new();
        if !peek_is_nil(&cur) {
            let mlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            for _ in 0..mlen {
                let k = read_mp_string(&mut cur)?;
                let mut ids = Vec::new();
                if !peek_is_nil(&cur) {
                    let alen =
                        read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                    for _ in 0..alen {
                        ids.push(read_mp_string(&mut cur)?);
                    }
                } else {
                    skip_mp_value(&mut cur)?;
                }
                materials.insert(k, ids);
            }
        }
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            tool_id,
            materials,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct BuildEstimationMsg {
    pub entity_id: String,
    pub level: i32,
    pub durability: f32,
    pub tags: BTreeMap<String, i32>,
    pub unrevealed_rare_tag_count: i32,
    pub size_x: u16,
    pub size_y: u16,
    pub rotation: i32,
    pub is_modular: bool,
}

impl BuildEstimationMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(128);
        write_array_len(&mut out, 5)?;
        write_i32(&mut out, self.level)?;
        write_f32(&mut out, self.durability)?;
        write_map_len(&mut out, self.tags.len() as u32)?;
        for (k, v) in &self.tags {
            write_str(&mut out, k)?;
            write_i32(&mut out, *v)?;
        }
        write_i32(&mut out, self.unrevealed_rare_tag_count)?;
        // ArtifactPreview (4 fields: Size, Rotation, ArtifactDisplay(15), IsModular)
        write_array_len(&mut out, 4)?;
        write_array_len(&mut out, 2)?;
        write_u16(&mut out, self.size_x)?;
        write_u16(&mut out, self.size_y)?;
        write_i32(&mut out, self.rotation)?;
        write_array_len(&mut out, 15)?;
        write_str(&mut out, &self.entity_id)?;
        write_i32(&mut out, 0)?;
        write_nil(&mut out)?;
        write_map_len(&mut out, 0)?;
        write_map_len(&mut out, 0)?;
        write_map_len(&mut out, 0)?;
        write_map_len(&mut out, 0)?;
        for _ in 0..8 {
            write_nil(&mut out)?;
        }
        write_bool(&mut out, self.is_modular)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WorkbenchMsg {
    pub entity_id: String,
    pub capacity: u32,
}

impl WorkbenchMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(48);
        write_array_len(&mut out, 4)?;
        write_str(&mut out, &self.entity_id)?;
        write_u32(&mut out, self.capacity)?;
        write_array_len(&mut out, 0)?; // Craftings
        write_array_len(&mut out, 0)?; // Crafteds
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CancelCraftingMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub crafting_id: String,
}

impl CancelCraftingMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let crafting_id = read_mp_string(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            crafting_id,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SetStorageItemMsg {
    pub key: String,
    pub value: Vec<u8>,
}

impl SetStorageItemMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let key = read_mp_string(&mut cur)?;
        let value = read_mp_bin(&mut cur)?;
        Ok(Self { key, value })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct EstateLicenseMsg {
    pub estate_id: String,
    pub owner_type: i32,
    pub owner_id: String,
    pub activated_at: f64,
    pub expires_at: Option<f64>,
    pub access_for_others: i32,
    pub size: i32,
    pub region_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
}

impl EstateLicenseMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 15)?;
        write_str(out, &self.estate_id)?;
        write_i32(out, self.owner_type)?;
        write_str(out, &self.owner_id)?;
        write_f64(out, self.activated_at)?;
        write_nil(out)?; // DepositRunsOutAt
        match self.expires_at {
            Some(v) => write_f64(out, v)?,
            None => write_nil(out)?,
        }
        write_nil(out)?; // Deposit
        // AccessRights (3 fields: ForOthers, ForFriends(nil), ForClanMembers(nil))
        write_array_len(out, 3)?;
        write_i32(out, self.access_for_others)?;
        write_nil(out)?;
        write_nil(out)?;
        write_i32(out, self.size)?;
        write_str(out, &self.region_id)?;
        write_array_len(out, 2)?;
        write_u16(out, self.tile_x)?;
        write_u16(out, self.tile_y)?;
        write_nil(out)?; // ProtectedUntil
        write_nil(out)?; // CycleStartsAt
        write_nil(out)?; // CycleEndsAt
        write_nil(out)?; // RemovableAt
        Ok(())
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(128);
        self.write_to(&mut out)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct EstateLicensesMsg {
    pub urban_estate: Option<EstateLicenseMsg>,
    pub largest_urban_estate_size: i32,
    pub personal_estate: Option<EstateLicenseMsg>,
    pub largest_personal_estate_size: i32,
    pub clan_estate: Option<EstateLicenseMsg>,
    pub largest_clan_estate_size: i32,
}

impl EstateLicensesMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(160);
        write_array_len(&mut out, 10)?;
        match &self.urban_estate {
            Some(e) => e.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        write_i32(&mut out, self.largest_urban_estate_size)?;
        match &self.personal_estate {
            Some(e) => e.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        write_i32(&mut out, self.largest_personal_estate_size)?;
        match &self.clan_estate {
            Some(e) => e.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        write_i32(&mut out, self.largest_clan_estate_size)?;
        write_nil(&mut out)?; // ClanCargoWarphole
        write_f64(&mut out, 0.0)?; // ClanCargoWarpholeVisitAvailableAt
        write_nil(&mut out)?; // PersonalWarpholeTile
        write_nil(&mut out)?; // UrbanWarpholeTile
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct PersonalRegionMsg {
    pub region: RegionMsg,
    pub owner_id: String,
    pub pioneer_exp: i32,
    pub admission_categories: Vec<i32>,
}

impl PersonalRegionMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 4)?;
        self.region.write_to(out)?;
        write_str(out, &self.owner_id)?;
        write_i32(out, self.pioneer_exp)?;
        write_array_len(out, self.admission_categories.len() as u32)?;
        for &cat in &self.admission_categories {
            write_i32(out, cat)?;
        }
        Ok(())
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(96);
        self.write_to(&mut out)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct PersonalRegionInfoMsg {
    pub personal_region: Option<PersonalRegionMsg>,
    pub personal_estate: Option<EstateLicenseMsg>,
}

impl PersonalRegionInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(160);
        write_array_len(&mut out, 2)?;
        match &self.personal_region {
            Some(pr) => pr.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        match &self.personal_estate {
            Some(est) => est.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct EstateGridsMsg {
    pub chunks: Vec<(u8, u8)>,
    pub cells: Vec<((u8, u8), String)>,
    pub estate_licenses: Vec<EstateLicenseMsg>,
}

impl EstateGridsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(128);
        write_array_len(&mut out, 3)?;
        write_array_len(&mut out, self.chunks.len() as u32)?;
        for &(cx, cy) in &self.chunks {
            write_array_len(&mut out, 2)?;
            write_u8(&mut out, cx)?;
            write_u8(&mut out, cy)?;
        }
        write_map_len(&mut out, self.cells.len() as u32)?;
        for ((cx, cy), eid) in &self.cells {
            write_array_len(&mut out, 2)?;
            write_u8(&mut out, *cx)?;
            write_u8(&mut out, *cy)?;
            write_str(&mut out, eid)?;
        }
        write_array_len(&mut out, self.estate_licenses.len() as u32)?;
        for lic in &self.estate_licenses {
            lic.write_to(&mut out)?;
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DeclareEstateMsg {
    pub owner_type: i32,
    pub cell_x: u8,
    pub cell_y: u8,
}

impl DeclareEstateMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(8);
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.owner_type)?;
        write_array_len(&mut out, 2)?;
        write_u8(&mut out, self.cell_x)?;
        write_u8(&mut out, self.cell_y)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let owner_type = read_mp_i32(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let cell_x = read_mp_u8(&mut cur)?;
        let cell_y = read_mp_u8(&mut cur)?;
        Ok(Self {
            owner_type,
            cell_x,
            cell_y,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EstateCellOpMsg {
    pub estate_id: String,
    pub cell_x: u8,
    pub cell_y: u8,
}

impl EstateCellOpMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let estate_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let cell_x = read_mp_u8(&mut cur)?;
        let cell_y = read_mp_u8(&mut cur)?;
        Ok(Self {
            estate_id,
            cell_x,
            cell_y,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ExtendEstateActivationMsg {
    pub estate_id: String,
    pub cost: i64,
}

impl ExtendEstateActivationMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let estate_id = read_mp_string(&mut cur)?;
        let cost: i64 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        Ok(Self { estate_id, cost })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SingleStringMsg {
    pub value: String,
}

impl SingleStringMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(16 + self.value.len());
        write_array_len(&mut out, 1)?;
        write_str(&mut out, &self.value)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let value = read_mp_string(&mut cur)?;
        Ok(Self { value })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReturnToEstateMsg {
    pub owner_type: i32,
}

impl ReturnToEstateMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let owner_type = read_mp_i32(&mut cur)?;
        Ok(Self { owner_type })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SetEstateLicenseMsg {
    pub estate_id: String,
    pub access_for_others: i32,
}

impl SetEstateLicenseMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let estate_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let access_for_others = read_mp_i32(&mut cur)?;
        Ok(Self {
            estate_id,
            access_for_others,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SetPersonalRegionAdmissionMsg {
    pub admission_categories: Vec<i32>,
}

impl SetPersonalRegionAdmissionMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
        let mut admission_categories = Vec::with_capacity(safe_cap(n, &cur));
        for _ in 0..n {
            admission_categories.push(read_mp_i32(&mut cur)?);
        }
        Ok(Self {
            admission_categories,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct PointEntityTileMsg {
    pub region: RegionMsg,
    pub tile_x: u16,
    pub tile_y: u16,
    pub entity_id: String,
    pub entity_name: String,
}

impl PointEntityTileMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 4)?;
        self.region.write_to(out)?;
        write_array_len(out, 2)?;
        write_u16(out, self.tile_x)?;
        write_u16(out, self.tile_y)?;
        write_str(out, &self.entity_id)?;
        write_str(out, &self.entity_name)?;
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct RegionTileMsg {
    pub region: RegionMsg,
    pub tile_x: u16,
    pub tile_y: u16,
}

impl RegionTileMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 2)?;
        self.region.write_to(out)?;
        write_array_len(out, 2)?;
        write_u16(out, self.tile_x)?;
        write_u16(out, self.tile_y)?;
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct PointsMsg {
    pub home_point: Option<PointEntityTileMsg>,
    pub returning_point: RegionTileMsg,
}

impl PointsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(160);
        write_array_len(&mut out, 5)?;
        match &self.home_point {
            Some(hp) => hp.write_to(&mut out)?,
            None => write_nil(&mut out)?,
        }
        self.returning_point.write_to(&mut out)?;
        write_nil(&mut out)?; // DeathPoint
        write_nil(&mut out)?; // LastReturnPoint
        write_nil(&mut out)?; // CampPoint
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PointOfInterestMsg {
    pub tile_x: u16,
    pub tile_y: u16,
    pub poi_type: i32,
    pub icon: Option<String>,
    pub title: Option<String>,
    pub entity_type: Option<u16>,
    pub is_explored: bool,
}

impl PointOfInterestMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 6)?;
        write_array_len(out, 2)?;
        write_u16(out, self.tile_x)?;
        write_u16(out, self.tile_y)?;
        write_i32(out, self.poi_type)?;
        write_mp_opt_str(out, &self.icon)?;
        write_mp_opt_str(out, &self.title)?;
        match self.entity_type {
            Some(et) => write_u16(out, et)?,
            None => write_nil(out)?,
        }
        write_bool(out, self.is_explored)?;
        Ok(())
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let poi_type = read_mp_i32(&mut cur)?;
        let icon = read_mp_opt_string(&mut cur)?;
        let title = read_mp_opt_string(&mut cur)?;
        let entity_type = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_u16(&mut cur)?)
        };
        let is_explored = read_bool(&mut cur).unwrap_or(true);
        Ok(Self {
            tile_x,
            tile_y,
            poi_type,
            icon,
            title,
            entity_type,
            is_explored,
        })
    }
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ExploredPoisMsg {
    pub pois: Vec<PointOfInterestMsg>,
    pub full_count_rewarded: bool,
    pub is_opened_map: bool,
}

impl ExploredPoisMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64 + self.pois.len() * 24);
        write_array_len(&mut out, 4)?;
        write_array_len(&mut out, self.pois.len() as u32)?;
        for poi in &self.pois {
            poi.write_to(&mut out)?;
        }
        write_bool(&mut out, self.full_count_rewarded)?;
        write_nil(&mut out)?; // RewardCost
        write_bool(&mut out, self.is_opened_map)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct PoiCountMsg {
    pub port_count: u8,
    pub warphole_count: u8,
    pub crater_count: u8,
    pub rift_count: u8,
}

impl PoiCountMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(8);
        write_array_len(&mut out, 4)?;
        write_u8(&mut out, self.port_count)?;
        write_u8(&mut out, self.warphole_count)?;
        write_u8(&mut out, self.crater_count)?;
        write_u8(&mut out, self.rift_count)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct LastSearchedTimeMsg {
    pub searched_at: f64,
}

impl LastSearchedTimeMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(12);
        write_array_len(&mut out, 1)?;
        write_f64(&mut out, self.searched_at)?;
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RequestNearestPoiMsg {
    pub poi_type: i32,
    pub tile_x: u16,
    pub tile_y: u16,
}

impl RequestNearestPoiMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let poi_type = read_mp_i32(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        Ok(Self {
            poi_type,
            tile_x,
            tile_y,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NearestPoiMsg {
    pub poi_type: i32,
    pub tile: Option<(u16, u16)>,
}

impl NearestPoiMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(16);
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.poi_type)?;
        match self.tile {
            Some((x, y)) => {
                write_array_len(&mut out, 2)?;
                write_u16(&mut out, x)?;
                write_u16(&mut out, y)?;
            }
            None => write_nil(&mut out)?,
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct PioneerGradeInfoMsg {
    pub entity_id: String,
    pub grade: i32,
    pub point: f32,
    pub point_needed: i32,
    pub current_maximum_estate_size: i32,
    pub current_access_level: i32,
}

impl PioneerGradeInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 9)?;
        write_str(&mut out, &self.entity_id)?;
        write_i32(&mut out, self.grade)?;
        write_f32(&mut out, self.point)?;
        write_i32(&mut out, self.point_needed)?;
        write_map_len(&mut out, 0)?; // DailyExchangedPoints
        write_i32(&mut out, self.current_maximum_estate_size)?;
        write_i32(&mut out, self.current_access_level)?;
        write_nil(&mut out)?; // PointAdded
        write_nil(&mut out)?; // PaymentEndsAt
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DiscoveryInfoMsg {
    pub template_id: String,
}

impl DiscoveryInfoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(32);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.template_id)?;
        write_array_len(&mut out, 0)?; // BiocomNames
        write_array_len(&mut out, 0)?; // AnimalTypes
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ArchipelagoMsg {
    pub id: String,
    pub template_id: String,
    pub unstable_factor: i32,
    pub name: String,
    pub expires_at: f64,
    pub included_region_ids: Vec<String>,
}

impl ArchipelagoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64 + self.included_region_ids.len() * 24);
        write_array_len(&mut out, 6)?;
        write_str(&mut out, &self.id)?;
        write_str(&mut out, &self.template_id)?;
        write_i32(&mut out, self.unstable_factor)?;
        write_str(&mut out, &self.name)?;
        write_f64(&mut out, self.expires_at)?;
        write_array_len(&mut out, self.included_region_ids.len() as u32)?;
        for rid in &self.included_region_ids {
            // IncludedRegion: [Id: str, ExplorationPercentage: i32, Pois: PointOfInterest[]]
            write_array_len(&mut out, 3)?;
            write_str(&mut out, rid)?;
            write_i32(&mut out, 100)?;
            write_array_len(&mut out, 0)?;
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArchipelagoRouteMsg {
    pub level: i32,
    pub biome: i32,
    pub unstable_factor: i32,
    pub archipelago_id: String,
    pub included_region_ids: Vec<String>,
}

impl ArchipelagoRouteMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        // ArchipelagoRoute (9 fields):
        // [Level: i32, Biome: i32, UnstableFactor: i32, ArchipelagoId: str, IncludedRoutes: Route[], Price?: nil, IsAlreadyFreeExplored: bool, FreeExploreInterval?: nil, FreeExploreLeftSeconds?: nil]
        write_array_len(out, 9)?;
        write_i32(out, self.level)?;
        write_i32(out, self.biome)?;
        write_i32(out, self.unstable_factor)?;
        write_str(out, &self.archipelago_id)?;
        write_array_len(out, self.included_region_ids.len() as u32)?;
        for rid in &self.included_region_ids {
            write_array_len(out, 2)?;
            write_str(out, rid)?;
            write_nil(out)?; // Price = free
        }
        write_nil(out)?; // Price = free
        write_bool(out, false)?; // IsAlreadyFreeExplored
        write_nil(out)?; // FreeExploreInterval
        write_nil(out)?; // FreeExploreLeftSeconds
        Ok(())
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct RoutesMsg {
    /// role -> template_id -> region_ids (price = None / free)
    pub by_role: BTreeMap<i32, BTreeMap<String, Vec<String>>>,
    pub archipelago_routes: Vec<ArchipelagoRouteMsg>,
}

impl RoutesMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(128 + self.archipelago_routes.len() * 48);
        write_array_len(&mut out, 2)?;
        write_map_len(&mut out, self.by_role.len() as u32)?;
        for (&role, by_tpl) in &self.by_role {
            write_i32(&mut out, role)?;
            write_map_len(&mut out, by_tpl.len() as u32)?;
            for (tpl, region_ids) in by_tpl {
                write_str(&mut out, tpl)?;
                write_array_len(&mut out, region_ids.len() as u32)?;
                for rid in region_ids {
                    write_array_len(&mut out, 2)?;
                    write_str(&mut out, rid)?;
                    write_nil(&mut out)?; // Price (free)
                }
            }
        }
        write_array_len(&mut out, self.archipelago_routes.len() as u32)?;
        for ar in &self.archipelago_routes {
            ar.write_to(&mut out)?;
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct RoutesOfArchipelagoMsg {
    pub archipelago_routes: Vec<ArchipelagoRouteMsg>,
}

impl RoutesOfArchipelagoMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(32 + self.archipelago_routes.len() * 48);
        write_array_len(&mut out, 1)?;
        write_array_len(&mut out, self.archipelago_routes.len() as u32)?;
        for ar in &self.archipelago_routes {
            ar.write_to(&mut out)?;
        }
        Ok(out)
    }
}

/// TravelByRegion (2029) — C->S `[EntityId: str, Tile: [u16, u16], RegionId: str, PartierId?: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TravelByRegionMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub region_id: String,
    pub partier_id: Option<String>,
}

impl TravelByRegionMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let (tile_x, tile_y) = if !peek_is_nil(&cur) {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            (read_mp_u16(&mut cur)?, read_mp_u16(&mut cur)?)
        } else {
            let _ = skip_mp_value(&mut cur)?;
            (0, 0)
        };
        let region_id = if len >= 3 {
            read_mp_string(&mut cur)?
        } else {
            String::new()
        };
        let partier_id = if len >= 4 {
            read_mp_opt_string(&mut cur)?
        } else {
            None
        };
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            region_id,
            partier_id,
        })
    }
}

/// TravelByRegionInArchipelago (2054) — C->S `[EntityId: str, Tile: [u16, u16], RegionId: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TravelByRegionInArchipelagoMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub region_id: String,
}

impl TravelByRegionInArchipelagoMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let (tile_x, tile_y) = if !peek_is_nil(&cur) {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            (read_mp_u16(&mut cur)?, read_mp_u16(&mut cur)?)
        } else {
            let _ = skip_mp_value(&mut cur)?;
            (0, 0)
        };
        let region_id = if len >= 3 {
            read_mp_string(&mut cur)?
        } else {
            String::new()
        };
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            region_id,
        })
    }
}

/// RecommendRegion (3001) — C->S `[EntityId?: str, Tile: [u16, u16], Role: i32, TemplateId?: str]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RecommendRegionMsg {
    pub entity_id: Option<String>,
    pub tile_x: u16,
    pub tile_y: u16,
    pub role: i32,
    pub template_id: Option<String>,
}

impl RecommendRegionMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = if len >= 1 {
            read_mp_opt_string(&mut cur)?
        } else {
            None
        };
        let (tile_x, tile_y) = if len >= 2 && !peek_is_nil(&cur) {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            (read_mp_u16(&mut cur)?, read_mp_u16(&mut cur)?)
        } else {
            if len >= 2 {
                let _ = skip_mp_value(&mut cur)?;
            }
            (0, 0)
        };
        let role = if len >= 3 {
            read_mp_i32(&mut cur)?
        } else {
            9
        };
        let template_id = if len >= 4 {
            read_mp_opt_string(&mut cur)?
        } else {
            None
        };
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            role,
            template_id,
        })
    }
}

/// RecommendArchipelago (3012) — C->S `[Level: i32, Biome: i32, UnstableFactor: i32]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RecommendArchipelagoMsg {
    pub level: i32,
    pub biome: i32,
    pub unstable_factor: i32,
}

impl RecommendArchipelagoMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let level = if len >= 1 { read_mp_i32(&mut cur)? } else { 15 };
        let biome = if len >= 2 { read_mp_i32(&mut cur)? } else { 5 };
        let unstable_factor = if len >= 3 { read_mp_i32(&mut cur)? } else { 1 };
        Ok(Self {
            level,
            biome,
            unstable_factor,
        })
    }
}

/// SailingBackCost (3120) — S->C `[Cost: i64]`
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct SailingBackCostMsg {
    pub cost: i64,
}

impl SailingBackCostMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(12);
        write_array_len(&mut out, 1)?;
        write_i64(&mut out, self.cost)?;
        Ok(out)
    }
}

/// SearchedPOIs (905) — S->C `[Results: [[TileX, TileY], PoiType][], SearchedAt: f64]`
#[derive(Debug, Clone, PartialEq)]
pub struct SearchedPoisMsg {
    pub results: Vec<(u16, u16, i32)>,
    pub searched_at: f64,
}

impl SearchedPoisMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(24 + self.results.len() * 12);
        write_array_len(&mut out, 2)?;
        write_array_len(&mut out, self.results.len() as u32)?;
        for &(tx, ty, ptype) in &self.results {
            write_array_len(&mut out, 2)?;
            write_array_len(&mut out, 2)?;
            write_u16(&mut out, tx)?;
            write_u16(&mut out, ty)?;
            write_i32(&mut out, ptype)?;
        }
        write_f64(&mut out, self.searched_at)?;
        Ok(out)
    }
}

/// PutMaterialsIntoArtifact (2092) — C->S `[EntityId: str, Tile: [u16, u16], Materials: map<str, str[]>]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PutMaterialsIntoArtifactMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub materials: BTreeMap<String, Vec<String>>,
}

impl PutMaterialsIntoArtifactMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let (tile_x, tile_y) = if len >= 2 && !peek_is_nil(&cur) {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            (read_mp_u16(&mut cur)?, read_mp_u16(&mut cur)?)
        } else {
            if len >= 2 {
                let _ = skip_mp_value(&mut cur)?;
            }
            (0, 0)
        };
        let mut materials = BTreeMap::new();
        if len >= 3 && !peek_is_nil(&cur) {
            let mlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            for _ in 0..mlen {
                let slot = read_mp_string(&mut cur)?;
                let alen = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                let mut ids = Vec::with_capacity(safe_cap(alen as usize, &cur));
                for _ in 0..alen {
                    ids.push(read_mp_string(&mut cur)?);
                }
                materials.insert(slot, ids);
            }
        }
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            materials,
        })
    }
}

/// ArtifactMaterials (2091) — S->C `[EntityId: str, Materials: map<str, Item[]>]`
#[derive(Debug, Clone, PartialEq)]
pub struct ArtifactMaterialsMsg {
    pub entity_id: String,
    pub materials: BTreeMap<String, Vec<ItemMsg>>,
}

impl ArtifactMaterialsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.entity_id)?;
        write_map_len(&mut out, self.materials.len() as u32)?;
        for (slot, items) in &self.materials {
            write_str(&mut out, slot)?;
            write_array_len(&mut out, items.len() as u32)?;
            for item in items {
                item.write_to(&mut out)?;
            }
        }
        Ok(out)
    }
}

/// SetRecipeLike (3800) / SetBlueprintLike (3801) — C->S `[Id: str, Like: bool]`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SetLikeMsg {
    pub id: String,
    pub like: bool,
}

impl SetLikeMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let id = read_mp_string(&mut cur)?;
        let like = read_bool(&mut cur).unwrap_or(false);
        Ok(Self { id, like })
    }
}

pub fn pack_empty_social() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(12);
    write_array_len(&mut out, 6)?;
    write_array_len(&mut out, 0)?; // FollowingEntityIds
    write_map_len(&mut out, 0)?;   // FriendEntities
    write_array_len(&mut out, 0)?; // ReceivedFriendRequests
    write_array_len(&mut out, 0)?; // SentFriendRequests
    write_array_len(&mut out, 0)?; // BlockedEntityIds
    write_array_len(&mut out, 0)?; // FavoriteRegionOwners
    Ok(out)
}

pub fn pack_empty_single_map() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(4);
    write_array_len(&mut out, 1)?;
    write_map_len(&mut out, 0)?;
    Ok(out)
}

pub fn pack_empty_single_array() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(4);
    write_array_len(&mut out, 1)?;
    write_array_len(&mut out, 0)?;
    Ok(out)
}

pub fn pack_empty_factions() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(12);
    write_array_len(&mut out, 2)?;
    write_array_len(&mut out, 0)?; // _Factions
    write_f64(&mut out, 0.0)?;     // DailyMissionAvailableAt
    Ok(out)
}

pub fn pack_empty_mission_infos() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(12);
    write_array_len(&mut out, 5)?;
    write_array_len(&mut out, 0)?; // Missions
    write_map_len(&mut out, 0)?;   // MissionActivatesAt
    write_nil(&mut out)?;          // RecommendFailReasons
    write_u8(&mut out, 0)?;        // ShuffleCount
    write_nil(&mut out)?;          // ShuffleAt
    Ok(out)
}

pub fn pack_empty_musics() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(6);
    write_array_len(&mut out, 2)?;
    write_map_len(&mut out, 0)?; // _Musics
    write_map_len(&mut out, 0)?; // SharedMusics
    Ok(out)
}

pub fn pack_empty_support_requests() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(18);
    write_array_len(&mut out, 5)?;
    write_array_len(&mut out, 0)?; // Requests
    write_f64(&mut out, 0.0)?;     // EndAt
    write_map_len(&mut out, 0)?;   // Untils
    write_array_len(&mut out, 0)?; // FactionTypes
    write_i32(&mut out, 0)?;       // Level
    Ok(out)
}

pub fn pack_empty_nomad_info() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(6);
    write_array_len(&mut out, 2)?;
    write_bool(&mut out, false)?;
    write_i32(&mut out, 0)?;
    Ok(out)
}

pub fn pack_empty_returner_info() -> Result<Vec<u8>, WireError> {
    let mut out = Vec::with_capacity(24);
    write_array_len(&mut out, 4)?;
    write_bool(&mut out, false)?;
    write_f64(&mut out, 0.0)?;
    write_f64(&mut out, 0.0)?;
    write_i32(&mut out, 0)?;
    Ok(out)
}

// ---------------------------------------------------------------------------
// Phase 2A Wire Messages: CombatInteraction, DumpItems, RepairItem, StatusEffects
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CombatInteractionMsg {
    pub entity_id: String,
    pub target_id: String,
    pub details: BTreeMap<String, i64>,
}

impl CombatInteractionMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.entity_id)?;
        write_str(&mut out, &self.target_id)?;
        write_map_len(&mut out, self.details.len() as u32)?;
        for (k, &v) in &self.details {
            write_str(&mut out, k)?;
            write_i64(&mut out, v)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let target_id = read_mp_string(&mut cur)?;
        let mut details = BTreeMap::new();
        if !peek_is_nil(&cur) {
            let n = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            for _ in 0..n {
                let k = read_mp_string(&mut cur)?;
                let v: i64 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
                details.insert(k, v);
            }
        }
        Ok(Self {
            entity_id,
            target_id,
            details,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DumpItemsMsg {
    pub item_ids: Vec<String>,
}

impl DumpItemsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(32 + self.item_ids.len() * 16);
        write_array_len(&mut out, 6)?;
        write_nil(&mut out)?; // SourceProp
        write_nil(&mut out)?; // SourcePetEntityId
        write_nil(&mut out)?; // SectionName
        write_array_len(&mut out, self.item_ids.len() as u32)?;
        for id in &self.item_ids {
            write_str(&mut out, id)?;
        }
        write_nil(&mut out)?; // Tile
        write_nil(&mut out)?; // Floor
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let _ = skip_mp_value(&mut cur)?; // SourceProp
        let _ = skip_mp_value(&mut cur)?; // SourcePetEntityId
        let _ = skip_mp_value(&mut cur)?; // SectionName
        let mut item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                item_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self { item_ids })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RepairArtifactMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub kit_item_ids: Vec<String>,
}

impl RepairArtifactMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.entity_id)?;
        write_array_len(&mut out, 2)?;
        write_u16(&mut out, self.tile_x)?;
        write_u16(&mut out, self.tile_y)?;
        write_array_len(&mut out, self.kit_item_ids.len() as u32)?;
        for id in &self.kit_item_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let arr_len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let mut tile_x = 0u16;
        let mut tile_y = 0u16;
        let mut kit_item_ids = Vec::new();
        if arr_len >= 2 {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            tile_x = read_mp_u16(&mut cur)?;
            tile_y = read_mp_u16(&mut cur)?;
        }
        if arr_len >= 3 && !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            kit_item_ids.reserve(safe_cap(n, &cur));
            for _ in 0..n {
                kit_item_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            kit_item_ids,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RepairImmediateMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub cost_amount: i32,
    pub cost_currency: i32,
}

impl RepairImmediateMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.entity_id)?;
        write_array_len(&mut out, 2)?;
        write_u16(&mut out, self.tile_x)?;
        write_u16(&mut out, self.tile_y)?;
        write_array_len(&mut out, 2)?;
        write_i32(&mut out, self.cost_amount)?;
        write_i32(&mut out, self.cost_currency)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let arr_len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let mut tile_x = 0u16;
        let mut tile_y = 0u16;
        let mut cost_amount = 0i32;
        let mut cost_currency = 0i32;
        if arr_len >= 2 {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            tile_x = read_mp_u16(&mut cur)?;
            tile_y = read_mp_u16(&mut cur)?;
        }
        if arr_len >= 3 && !peek_is_nil(&cur) {
            let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
            cost_amount = read_mp_i32(&mut cur)?;
            cost_currency = read_mp_i32(&mut cur)?;
        }
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            cost_amount,
            cost_currency,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct MountAirBalloonMsg {
    pub with_voucher: Option<bool>,
}

impl MountAirBalloonMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(8);
        write_array_len(&mut out, 1)?;
        match self.with_voucher {
            Some(v) => write_bool(&mut out, v)?,
            None => write_nil(&mut out)?,
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let arr_len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let with_voucher = if arr_len == 0 || peek_is_nil(&cur) {
            None
        } else {
            Some(read_bool(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?)
        };
        Ok(Self { with_voucher })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RepairItemMsg {
    pub item_id: String,
    pub kit_item_ids: Vec<String>,
}

impl RepairItemMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(48);
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.item_id)?;
        write_array_len(&mut out, self.kit_item_ids.len() as u32)?;
        for id in &self.kit_item_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let item_id = read_mp_string(&mut cur)?;
        let mut kit_item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                kit_item_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self {
            item_id,
            kit_item_ids,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct StatusEffectMsg {
    pub id: String,
    pub effect_id: String,
    pub level: i32,
    pub since: f64,
    pub until: f64,
    pub stacked: i32,
    pub duration_hidden: bool,
    pub name_gettext: Option<String>,
}

impl StatusEffectMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 10)?;
        write_str(out, &self.id)?;
        write_str(out, &self.effect_id)?;
        write_i32(out, self.level)?;
        write_f64(out, self.since)?;
        write_f64(out, self.until)?;
        write_i32(out, self.stacked)?;
        write_bool(out, self.duration_hidden)?;
        write_mp_opt_str(out, &self.name_gettext)?;
        write_array_len(out, 0)?; // Effects
        write_nil(out)?; // DailyContents
        Ok(())
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct StatusEffectsMsg {
    pub entity_id: String,
    pub effects: Vec<StatusEffectMsg>,
}

impl StatusEffectsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64 + self.effects.len() * 48);
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.entity_id)?;
        write_array_len(&mut out, self.effects.len() as u32)?;
        for ef in &self.effects {
            ef.write_to(&mut out)?;
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ToggleStatusEffectMsg {
    pub id: String,
    pub toggle: bool,
}

impl ToggleStatusEffectMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let id = read_mp_string(&mut cur)?;
        let toggle = read_bool(&mut cur).unwrap_or(false);
        Ok(Self { id, toggle })
    }
}

// ---------------------------------------------------------------------------
// Phase 2B Wire Messages: WalletUpdated, Storage/Warehouse, Market Tabs, Mail
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WalletUpdatedMsg {
    pub entity_id: String,
    pub t_stone: i64,
}

impl WalletUpdatedMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(48);
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.entity_id)?;
        write_wallet(&mut out, self.t_stone)?;
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let t_stone = read_wallet_tstone(&mut cur)?;
        Ok(Self { entity_id, t_stone })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GetInventoryMsg {
    pub target_entity_id: Option<String>,
}

impl GetInventoryMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).unwrap_or(0);
        if len == 0 || peek_is_nil(&cur) {
            return Ok(Self {
                target_entity_id: None,
            });
        }
        // PropKey = [EntityId, [TileX, TileY]]
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let eid = read_mp_string(&mut cur)?;
        Ok(Self {
            target_entity_id: if eid.is_empty() { None } else { Some(eid) },
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ContainerItemsOpMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub item_ids: Vec<String>,
}

impl ContainerItemsOpMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(48);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.entity_id)?;
        write_array_len(&mut out, 2)?;
        write_u16(&mut out, self.tile_x)?;
        write_u16(&mut out, self.tile_y)?;
        write_array_len(&mut out, self.item_ids.len() as u32)?;
        for id in &self.item_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let mut item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                item_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            item_ids,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WarehouseSectionInfoMsg {
    pub section_name: String,
    pub used_size: i32,
    pub max_size: i32,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WarehouseMsg {
    pub entity_id: String,
    pub sections: Vec<WarehouseSectionInfoMsg>,
}

impl WarehouseMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 2)?;
        write_str(&mut out, &self.entity_id)?;
        write_array_len(&mut out, self.sections.len() as u32)?;
        for s in &self.sections {
            write_array_len(&mut out, 3)?;
            write_str(&mut out, &s.section_name)?;
            write_i32(&mut out, s.used_size)?;
            write_i32(&mut out, s.max_size)?;
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WarehouseSectionOpMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub section_name: String,
}

impl WarehouseSectionOpMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let section_name = read_mp_string(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            section_name,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RenameWarehouseSectionMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub section_name: String,
    pub new_name: String,
}

impl RenameWarehouseSectionMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let section_name = read_mp_string(&mut cur)?;
        let new_name = read_mp_string(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            section_name,
            new_name,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WarehouseItemsOpMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub section_name: String,
    pub item_ids: Vec<String>,
}

impl WarehouseItemsOpMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let section_name = read_mp_string(&mut cur)?;
        let mut item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                item_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            section_name,
            item_ids,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct MoveItemsInWarehouseMsg {
    pub entity_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub item_ids: Vec<String>,
    pub source_section: String,
    pub target_section: String,
}

impl MoveItemsInWarehouseMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let entity_id = read_mp_string(&mut cur)?;
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let tile_x = read_mp_u16(&mut cur)?;
        let tile_y = read_mp_u16(&mut cur)?;
        let mut item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                item_ids.push(read_mp_string(&mut cur)?);
            }
        } else {
            skip_mp_value(&mut cur)?;
        }
        let source_section = read_mp_string(&mut cur)?;
        let target_section = read_mp_string(&mut cur)?;
        Ok(Self {
            entity_id,
            tile_x,
            tile_y,
            item_ids,
            source_section,
            target_section,
        })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct RegisterMultipleProductsMsg {
    pub item_ids: Vec<String>,
    pub each_price: i64,
    pub duration: f32,
}

impl RegisterMultipleProductsMsg {
    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                item_ids.push(read_mp_string(&mut cur)?);
            }
        } else {
            skip_mp_value(&mut cur)?;
        }
        let each_price: i64 = read_int(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let duration = read_mp_f32(&mut cur)?;
        Ok(Self {
            item_ids,
            each_price,
            duration,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SendMailMsg {
    pub recipient_id: String,
    pub text: String,
    pub item_ids: Vec<String>,
}

impl SendMailMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 3)?;
        write_str(&mut out, &self.recipient_id)?;
        write_str(&mut out, &self.text)?;
        write_array_len(&mut out, self.item_ids.len() as u32)?;
        for id in &self.item_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let recipient_id = read_mp_string(&mut cur)?;
        let text = read_mp_string(&mut cur)?;
        let mut item_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                item_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self {
            recipient_id,
            text,
            item_ids,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct MailIdsMsg {
    pub mail_ids: Vec<String>,
}

impl MailIdsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(32);
        write_array_len(&mut out, 1)?;
        write_array_len(&mut out, self.mail_ids.len() as u32)?;
        for id in &self.mail_ids {
            write_str(&mut out, id)?;
        }
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let _ = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut mail_ids = Vec::new();
        if !peek_is_nil(&cur) {
            let n = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))? as usize;
            for _ in 0..n {
                mail_ids.push(read_mp_string(&mut cur)?);
            }
        }
        Ok(Self { mail_ids })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct MailMsg {
    pub id: String,
    pub sent_at: f64,
    pub sender_id: String,
    pub mail_type: i32,
    pub text: String,
    pub t_stone: i32,
    pub attached_items: Vec<ItemMsg>,
    pub accepted: bool,
    pub read: bool,
    pub expires_at: Option<f64>,
}

impl MailMsg {
    pub fn write_to(&self, out: &mut Vec<u8>) -> Result<(), WireError> {
        write_array_len(out, 14)?;
        write_str(out, &self.id)?;
        write_f64(out, self.sent_at)?;
        write_str(out, &self.sender_id)?;
        write_i32(out, self.mail_type)?;
        write_str(out, &self.text)?;
        if self.t_stone > 0 {
            write_map_len(out, 1)?;
            write_i32(out, 0)?; // Currency::TStone
            write_i32(out, self.t_stone)?;
        } else {
            write_map_len(out, 0)?;
        }
        write_array_len(out, self.attached_items.len() as u32)?;
        for it in &self.attached_items {
            it.write_to(out)?;
        }
        write_array_len(out, 0)?; // AttachedVouchers
        write_bool(out, self.accepted)?;
        write_bool(out, self.read)?;
        write_nil(out)?; // ClanId
        match self.expires_at {
            Some(v) => write_f64(out, v)?,
            None => write_nil(out)?,
        }
        write_nil(out)?; // AcceptedEntityId
        write_bool(out, false)?; // Highlighted
        Ok(())
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct MailsMsg {
    pub mails: Vec<MailMsg>,
    pub user_mails: Vec<MailMsg>,
}

impl MailsMsg {
    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(64);
        write_array_len(&mut out, 2)?;
        write_array_len(&mut out, self.mails.len() as u32)?;
        for m in &self.mails {
            m.write_to(&mut out)?;
        }
        write_array_len(&mut out, self.user_mails.len() as u32)?;
        for m in &self.user_mails {
            m.write_to(&mut out)?;
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct FatigueVelocitiesMsg {
    /// Map of `FatigueCategory` (0 = Default) -> velocity per second (negative = recovering while resting).
    pub velocities: BTreeMap<i32, f32>,
    pub fatigue_effect: Option<String>,
    pub resistances: BTreeMap<i32, f32>,
}

impl FatigueVelocitiesMsg {
    pub fn resting(rate_per_sec: f32) -> Self {
        let mut velocities = BTreeMap::new();
        velocities.insert(0, -rate_per_sec.abs());
        Self {
            velocities,
            fatigue_effect: None,
            resistances: BTreeMap::new(),
        }
    }

    pub fn idle() -> Self {
        let mut velocities = BTreeMap::new();
        velocities.insert(0, 0.0);
        Self {
            velocities,
            fatigue_effect: None,
            resistances: BTreeMap::new(),
        }
    }

    pub fn pack(&self) -> Result<Vec<u8>, WireError> {
        let mut out = Vec::with_capacity(32);
        write_array_len(&mut out, 4)?;
        write_map_len(&mut out, self.velocities.len() as u32)?;
        for (&cat, &vel) in &self.velocities {
            rmp::encode::write_i32(&mut out, cat)?;
            write_f32(&mut out, vel)?;
        }
        write_mp_opt_str(&mut out, &self.fatigue_effect)?;
        write_map_len(&mut out, self.resistances.len() as u32)?;
        for (&cat, &res) in &self.resistances {
            rmp::encode::write_i32(&mut out, cat)?;
            write_f32(&mut out, res)?;
        }
        write_nil(&mut out)?; // BiomeFatigue?
        Ok(out)
    }

    pub fn unpack(bytes: &[u8]) -> Result<Self, WireError> {
        let mut cur = Cursor::new(bytes);
        let len = read_array_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        if len < 4 {
            return Err(WireError::Decode("FatigueVelocities len < 4".into()));
        }
        let vlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut velocities = BTreeMap::new();
        for _ in 0..vlen {
            let k = read_mp_i32(&mut cur)?;
            let v = read_mp_f32(&mut cur)?;
            velocities.insert(k, v);
        }
        let fatigue_effect = if peek_is_nil(&cur) {
            skip_mp_value(&mut cur)?;
            None
        } else {
            Some(read_mp_string(&mut cur)?)
        };
        let rlen = read_map_len(&mut cur).map_err(|e| WireError::Decode(e.to_string()))?;
        let mut resistances = BTreeMap::new();
        for _ in 0..rlen {
            let k = read_mp_i32(&mut cur)?;
            let v = read_mp_f32(&mut cur)?;
            resistances.insert(k, v);
        }
        skip_mp_value(&mut cur)?; // BiomeFatigue?
        Ok(Self {
            velocities,
            fatigue_effect,
            resistances,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn roundtrip_snappy_msgpack_frames_and_partial_reads() {
        let auth = Auth {
            entity_id: "ent-1".into(),
            session_token: "tok-abc".into(),
            client_version: "5.2.1".into(),
            device_model: "LinuxWine".into(),
        };
        let frame = encode_frame(1700000000.5, 7, 0, TC_AUTH, &auth.pack().unwrap()).unwrap();
        assert!(try_decode_frame(&frame[..10]).unwrap().is_none());
        assert!(try_decode_frame(&frame[..frame.len() - 1]).unwrap().is_none());
        let (decoded, used) = try_decode_frame(&frame).unwrap().unwrap();
        assert_eq!(used, frame.len());
        assert_eq!(decoded.header.seq, 7);
        assert_eq!(decoded.header.type_code, TC_AUTH);
        assert_eq!(Auth::unpack(&decoded.msgpack).unwrap(), auth);

        let welcome = Welcome {
            user_id: "ent-1".into(),
            name: "Pioneer".into(),
            region: RegionMsg {
                id: "pe10gr_1".into(),
                terrain_id: "pe10gr_1".into(),
                template_id: "pe10gr_1".into(),
                role: RegionRole::Rural,
                name: None,
                created_at: 0.0,
            },
            storage: BTreeMap::new(),
            bool_options: vec![("market.ui_enabled".into(), true)],
            int_options: vec![("market.search.limit".into(), 20)],
            personal_region_id: None,
            engagement_reward_sent: false,
        };
        let wframe = encode_frame(1700000001.0, 2, 7, TC_WELCOME, &welcome.pack().unwrap()).unwrap();
        let (wdec, _) = try_decode_frame(&wframe).unwrap().unwrap();
        assert_eq!(wdec.header.reply_of, 7);
        assert_eq!(Welcome::unpack(&wdec.msgpack).unwrap(), welcome);

        let chunk = ChunkMsg {
            x: 8,
            y: 9,
            garden: vec![1, 2, 3, 4],
        };
        let cframe = encode_frame(1.0, 3, 0, TC_CHUNK, &chunk.pack().unwrap()).unwrap();
        let (cdec, _) = try_decode_frame(&cframe).unwrap().unwrap();
        assert_eq!(ChunkMsg::unpack(&cdec.msgpack).unwrap(), chunk);

        let mv = MoveMsg {
            entity_id: "ent-1".into(),
            movements: vec![Movement {
                motion_name: "run".into(),
                motion_option: 0,
                playback_rate: 1.0,
                rot_speed: 6.0,
                path: vec![Location {
                    x: 64.0,
                    y: 72.0,
                    yaw: 1.5,
                    time: 100.25,
                    floor: 0,
                    height: 0.0,
                }],
            }],
        };
        let mframe = encode_frame(2.0, 4, 0, TC_MOVE, &mv.pack().unwrap()).unwrap();
        let (mdec, _) = try_decode_frame(&mframe).unwrap().unwrap();
        assert_eq!(MoveMsg::unpack(&mdec.msgpack).unwrap(), mv);
    }

    #[test]
    fn roundtrip_step4_inventory_gathering_survival_and_chat() {
        let item = ItemMsg {
            id: "item-1".into(),
            name: "Wood Bough".into(),
            description: String::new(),
            icon: "icon_nat_wood_log".into(),
            sub_icon: None,
            prototype: "wood_bough".into(),
            level: 3,
            original_level: 3,
            size: 1,
            durability: GaugeMsg::constant(10.0, 100.0, 100.0),
            founder_id: "ent-1".into(),
            tags: vec![
                ItemTagMsg {
                    id: "wood".into(),
                    level: 1,
                },
                ItemTagMsg {
                    id: "stick_normal".into(),
                    level: 1,
                },
            ],
            performances: vec![],
            collectible_id: Some("tree_pine".into()),
            generator_id: Some("wood_bough".into()),
            tradable: true,
        };
        let inv = InventoryMsg {
            entity_id: "ent-1".into(),
            items: vec![item.clone()],
            infos: InventoryInfosMsg {
                entity_id: "ent-1".into(),
                max_size: 200,
                locked_item_ids: vec!["item-1".into()],
                item_order: vec!["item-1".into()],
            },
            t_stone: 50_000,
        };
        let inv_raw = inv.pack().unwrap();
        assert_eq!(InventoryMsg::unpack(&inv_raw).unwrap(), inv);

        let touched = TouchedMsg {
            entity_id: String::new(),
            entity_name: "tree_pine".into(),
            prototype_id: "tree_pine".into(),
            level: 1,
            interactions: vec![INTERACTION_COLLECT],
            collectible: CollectibleMsg {
                entity_id: String::new(),
                collectible_id: Some("tree_pine".into()),
                size: "medium".into(),
                generators: vec![GeneratorMsg {
                    id: "wood_bough".into(),
                    level: 1,
                    name: "Bough".into(),
                    icon: "icon_nat_wood_log".into(),
                    amount: 3,
                    effort: 10.0,
                    duration: 0.5,
                    tool_requirements: BTreeMap::new(),
                    enabled: true,
                }],
                critical_generator: String::new(),
            },
        };
        assert_eq!(TouchedMsg::unpack(&touched.pack().unwrap()).unwrap(), touched);

        let collected = CollectedMsg {
            items: vec![item],
            result: RESULT_SUCCESS,
            action_level: 1,
            potential_level: 1,
            success_ratio: 1.0,
            ran_out: true,
        };
        assert_eq!(CollectedMsg::unpack(&collected.pack().unwrap()).unwrap(), collected);

        let say = SayInExclusiveChannelMsg {
            entity_id: "ent-1".into(),
            time: 123.5,
            text: "Hello Wild Lands".into(),
            speaker_name: Some("Pioneer".into()),
            speaker_freq: 0,
            channel_type: 0,
        };
        assert_eq!(SayInExclusiveChannelMsg::unpack(&say.pack().unwrap()).unwrap(), say);

        let eq = EquipMsg {
            slot_name: "weapon".into(),
            slot_type: EQUIP_SLOT_1,
            item_id: "item-1".into(),
            action: "equip".into(),
        };
        assert_eq!(EquipMsg::unpack(&eq.pack().unwrap()).unwrap(), eq);

        let use_msg = UseItemMsg {
            item_id: "berry-1".into(),
            accept: true,
        };
        assert_eq!(UseItemMsg::unpack(&use_msg.pack().unwrap()).unwrap(), use_msg);

        let mut mats = BTreeMap::new();
        mats.insert("blade".into(), vec!["stone-1".into()]);
        let craft = CraftMsg {
            recipe_id: "stone_knife".into(),
            materials: mats,
            tool_item_id: None,
            reform_slot_index: None,
        };
        assert_eq!(CraftMsg::unpack(&craft.pack().unwrap()).unwrap(), craft);

        let crafted = CraftedMsg {
            result: RESULT_SUCCESS,
            action_level: 1,
            potential_level: 1,
            success_ratio: 1.0,
            items: vec![collected.items[0].clone()],
        };
        assert_eq!(CraftedMsg::unpack(&crafted.pack().unwrap()).unwrap(), crafted);

        let upd = InventoryUpdatedMsg {
            entity_id: "ent-1".into(),
            items: vec![collected.items[0].clone()],
            removed_item_ids: vec!["stone-1".into()],
            item_order: None,
        };
        assert_eq!(InventoryUpdatedMsg::unpack(&upd.pack().unwrap()).unwrap(), upd);

        let occ_req = OccupyArtifactSiteMsg {
            blueprint_id: Some("tent".into()),
            item_id: None,
            tile_x: 65,
            tile_y: 72,
            floor: None,
            size_x: 2,
            size_y: 2,
            stories: None,
            rotation: 0,
            modular_entity_id: String::new(),
        };
        assert_eq!(
            OccupyArtifactSiteMsg::unpack(&occ_req.pack().unwrap()).unwrap(),
            occ_req
        );

        let app_art = AppearArtifactMsg {
            entity_id: "art-1".into(),
            entity_type: 6151,
            is_alive: true,
            tile_x: 65,
            tile_y: 72,
            size_x: 2,
            size_y: 2,
            height: 1,
            rotation: 0,
            building_state: BUILDING_STATE_COMPLETED,
            founder_entity_id: "ent-1".into(),
            level: 1,
            now_secs: 50.0,
            parts: BTreeMap::from([("main".into(), "s02_shelter_01".into())]),
            condition: 0,
            durability: 100.0,
            max_durability: 100.0,
            gate_opened: false,
            tags: vec![ItemTagMsg {
                id: "table".into(),
                level: 1,
            }],
        };
        assert_eq!(
            AppearArtifactMsg::unpack(&app_art.pack().unwrap()).unwrap(),
            app_art
        );

        let destr = DestructingMsg {
            duration: 0.1,
            tool_type: 0,
        };
        assert_eq!(DestructingMsg::unpack(&destr.pack().unwrap()).unwrap(), destr);

        let animal = AppearAnimalMsg {
            entity_id: "dino-1".into(),
            entity_type: 2048,
            is_alive: true,
            base_scale: 1.0,
            level: 5,
            motion_name: "Para_Stand".into(),
            motion_option: 1,
            pos_x: 13000.0,
            pos_y: 14400.0,
            yaw: 0.0,
            hp: 80.0,
            max_hp: 80.0,
            role: None,
            enemy_id: None,
            now_secs: 99.0,
        };
        assert_eq!(AppearAnimalMsg::unpack(&animal.pack().unwrap()).unwrap(), animal);

        let uba = UseBattleActionMsg {
            action_id: "barehand_attack".into(),
            start_at: 100.0,
            target_entity_id: Some("dino-1".into()),
            target_tile: None,
            move_cancel_mode: None,
        };
        assert_eq!(UseBattleActionMsg::unpack(&uba.pack().unwrap()).unwrap(), uba);

        let dmg = DamagedMsg {
            victim_id: "dino-1".into(),
            attacker_id: "ent-1".into(),
            damage: DamageMsg {
                result: DAMAGE_RESULT_HIT,
                value: 35,
                part: BODY_PART_BODY,
                direction: DAMAGE_DIR_FRONT,
                attack_type: ATTACK_TYPE_BARE_HANDS,
                effects: 0,
            },
            event_at: 100.2,
        };
        assert_eq!(DamagedMsg::unpack(&dmg.pack().unwrap()).unwrap(), dmg);

        let stats = StatisticsMsg { level: 5, exp: 120 };
        assert_eq!(StatisticsMsg::unpack(&stats.pack().unwrap()).unwrap(), stats);
        let sk = SkillsMsg {
            skill_list: vec![SkillBundleMsg {
                category: SKILL_CAT_GATHERING,
                skill_id: "gathering".into(),
                levels: BTreeMap::from([("__base__".into(), 1)]),
            }],
            skill_point: 3,
            categories: BTreeMap::from([(
                SKILL_CAT_GATHERING,
                SkillCategoryMsg { level: 2, exp: 45 },
            )]),
            untrained_count: 0,
        };
        assert_eq!(SkillsMsg::unpack(&sk.pack().unwrap()).unwrap(), sk);
        let ls = LearnSkillMsg {
            skill_id: "gathering".into(),
            level: 1,
            sub_id: "__base__".into(),
        };
        assert_eq!(LearnSkillMsg::unpack(&ls.pack().unwrap()).unwrap(), ls);
        let us = UntrainSkillMsg {
            skill_id: "gathering".into(),
            level: 1,
            sub_id: "__base__".into(),
            voucher_id: None,
        };
        assert_eq!(UntrainSkillMsg::unpack(&us.pack().unwrap()).unwrap(), us);
        let eg = ExpGainedMsg {
            entity_id: "ent-1".into(),
            exp: 25,
            bonus_exp: 0,
            resistance_exp: 0,
        };
        assert_eq!(ExpGainedMsg::unpack(&eg.pack().unwrap()).unwrap(), eg);
        let q = QuestsMsg {
            category: "daily".into(),
        };
        assert_eq!(QuestsMsg::unpack(&q.pack().unwrap()).unwrap(), q);
        let pets = PetsInfoMsg { grazable_count: 2 };
        assert_eq!(PetsInfoMsg::unpack(&pets.pack().unwrap()).unwrap(), pets);
        let acc = AttachableAccessoriesMsg {
            accessories: vec![],
        };
        assert_eq!(
            AttachableAccessoriesMsg::unpack(&acc.pack().unwrap()).unwrap(),
            acc
        );
        let emo = AvailableEmotionsMsg {
            motions: vec!["wave".into()],
            emoticons: vec!["smile".into()],
        };
        assert_eq!(
            AvailableEmotionsMsg::unpack(&emo.pack().unwrap()).unwrap(),
            emo
        );
        let cf = CheatFlagsMsg {
            flags: BTreeMap::new(),
        };
        assert_eq!(CheatFlagsMsg::unpack(&cf.pack().unwrap()).unwrap(), cf);

        let rmi = RegionMapInfoMsg {
            region_id: "pe10gr_1".into(),
            terrain_id: "pe10gr_1".into(),
            tile_count_x: 256,
            tile_count_y: 256,
            defogged_chunks: vec![(0, 0), (4, 4)],
        };
        assert_eq!(RegionMapInfoMsg::unpack(&rmi.pack().unwrap()).unwrap(), rmi);

        let w = WarpMsg { tile_x: 70, tile_y: 80 };
        assert_eq!(WarpMsg::unpack(&w.pack().unwrap()).unwrap(), w);

        let rp = RegisterProductMsg {
            item_id: "item-1".into(),
            price: 150,
            duration: 24.0,
        };
        assert_eq!(RegisterProductMsg::unpack(&rp.pack().unwrap()).unwrap(), rp);

        let bp = BuyProductMsg {
            product_id: "prod-1".into(),
            payment_ids: vec![],
        };
        assert_eq!(BuyProductMsg::unpack(&bp.pack().unwrap()).unwrap(), bp);

        let sp = SearchProductsMsg {
            item_name: Some("Meat".into()),
            prototype_id: None,
            skip: 0,
        };
        assert_eq!(SearchProductsMsg::unpack(&sp.pack().unwrap()).unwrap(), sp);

        let prods = ProductsMsg {
            products: vec![ProductMsg {
                id: "prod-1".into(),
                region_id: "pe10gr_1".into(),
                listed_at: 10.0,
                expires_at: 100.0,
                deletes_at: 200.0,
                price: 150,
                items: vec![collected.items[0].clone()],
                level: 3,
                durability: 100.0,
            }],
        };
        assert_eq!(ProductsMsg::unpack(&prods.pack().unwrap()).unwrap(), prods);

        let mem = MemberMsg {
            entity_id: "ent-1".into(),
            clan_id: "clan-1".into(),
            clan_name: "Pioneers".into(),
            role_id: 1,
            applying_clan_id: None,
        };
        assert_eq!(MemberMsg::unpack(&mem.pack().unwrap()).unwrap(), mem);
        let rsr = RecommendedStableRegionsMsg {
            region_ids: vec!["pe10tr_1".into(), "pe10gr_2".into()],
        };
        assert_eq!(
            RecommendedStableRegionsMsg::unpack(&rsr.pack().unwrap()).unwrap(),
            rsr
        );
        let em = EmigratedMsg { teleport_type: 0 };
        assert_eq!(EmigratedMsg::unpack(&em.pack().unwrap()).unwrap(), em);

        let rev = ReviveMsg {
            warphole_tile: Some((63, 71)),
        };
        assert_eq!(ReviveMsg::unpack(&rev.pack().unwrap()).unwrap(), rev);
        let rev_nil = ReviveMsg {
            warphole_tile: None,
        };
        assert_eq!(
            ReviveMsg::unpack(&rev_nil.pack().unwrap()).unwrap(),
            rev_nil
        );
        let rev_imm = ReviveImmediatelyMsg {
            voucher_id: Some("voucher-1".into()),
        };
        assert_eq!(
            ReviveImmediatelyMsg::unpack(&rev_imm.pack().unwrap()).unwrap(),
            rev_imm
        );
        let rev_info = ReviveImmediatelyInfoMsg {
            used_count: 1,
            cost_amount: 0,
            cost_currency: 0,
        };
        assert_eq!(
            ReviveImmediatelyInfoMsg::unpack(&rev_info.pack().unwrap()).unwrap(),
            rev_info
        );
        let erev = EntityRevivedMsg {
            entity_id: "ent-1".into(),
            at: 123.45,
        };
        assert_eq!(EntityRevivedMsg::unpack(&erev.pack().unwrap()).unwrap(), erev);
        let tp = TeleportedMsg {
            tile_x: 63,
            tile_y: 71,
            teleport_type: TELEPORT_TYPE_REVIVE,
        };
        assert_eq!(TeleportedMsg::unpack(&tp.pack().unwrap()).unwrap(), tp);
        let ru = RunUpMsg { kind: 1, on: 1 };
        assert_eq!(RunUpMsg::unpack(&ru.pack().unwrap()).unwrap(), ru);
        let rd = RunDownMsg {
            kind: 0,
            json: "{\"enabled\":true,\"running\":true,\"mult\":1.45,\"reason\":\"\"}".into(),
        };
        assert_eq!(RunDownMsg::unpack(&rd.pack().unwrap()).unwrap(), rd);
        let spd = SetBaseMoveSpeedMsg {
            entity_id: "ent-1".into(),
            normal_speed: 660,
            battle_speed: 480,
        };
        assert_eq!(SetBaseMoveSpeedMsg::unpack(&spd.pack().unwrap()).unwrap(), spd);
        let est_req = EstimateCraftMsg {
            recipe_id: "stone_knife".into(),
            materials: BTreeMap::from([("material".into(), vec!["item-1".into()])]),
            tool_item_id: None,
        };
        assert_eq!(
            EstimateCraftMsg::unpack(&est_req.pack().unwrap()).unwrap(),
            est_req
        );
        let est_resp = CraftEstimationInfoMsg {
            craft_level: 5,
            craft_estimation: Some(CraftEstimationMsg {
                prototype_id: "tool_stone_knife".into(),
                level: 3,
                name: "Stone Knife".into(),
                durability_min: 90.0,
                durability_max: 110.0,
                tags: BTreeMap::from([("blade".into(), 1)]),
                unrevealed_rare_tag_count: 0,
                modifiable_count: 1,
                success_rate: 1.0,
                great_success_rate: 0.1,
                required_ability_value: 0.0,
            }),
        };
        assert_eq!(
            CraftEstimationInfoMsg::unpack(&est_resp.pack().unwrap()).unwrap(),
            est_resp
        );

        // SetStorageItemMsg must decode both MsgPack v5 bin (0xc4) and Unity MsgPack Classic raw/str (0xa0..0xbf, 0xda)
        let mut v5_bin = Vec::new();
        write_array_len(&mut v5_bin, 2).unwrap();
        write_str(&mut v5_bin, "guide_progress_personal").unwrap();
        write_bin(&mut v5_bin, b"{\"CompletedEvents\":[\"a\"]}").unwrap();
        let dec_v5 = SetStorageItemMsg::unpack(&v5_bin).unwrap();
        assert_eq!(dec_v5.key, "guide_progress_personal");
        assert_eq!(dec_v5.value, b"{\"CompletedEvents\":[\"a\"]}");

        let mut classic_raw = Vec::new();
        write_array_len(&mut classic_raw, 2).unwrap();
        write_str(&mut classic_raw, "guide_progress_personal").unwrap();
        write_str(&mut classic_raw, "{\"CompletedEvents\":[\"a\"]}").unwrap();
        let dec_classic = SetStorageItemMsg::unpack(&classic_raw).unwrap();
        assert_eq!(dec_classic.key, "guide_progress_personal");
        assert_eq!(dec_classic.value, b"{\"CompletedEvents\":[\"a\"]}");

        let rep_art = RepairArtifactMsg {
            entity_id: "art-1".into(),
            tile_x: 78,
            tile_y: 70,
            kit_item_ids: vec!["kit-1".into()],
        };
        assert_eq!(RepairArtifactMsg::unpack(&rep_art.pack().unwrap()).unwrap(), rep_art);

        let rep_imm = RepairImmediateMsg {
            entity_id: "art-1".into(),
            tile_x: 78,
            tile_y: 70,
            cost_amount: 120,
            cost_currency: 1,
        };
        assert_eq!(RepairImmediateMsg::unpack(&rep_imm.pack().unwrap()).unwrap(), rep_imm);

        for with_voucher in [None, Some(true), Some(false)] {
            let msg = MountAirBalloonMsg { with_voucher };
            assert_eq!(MountAirBalloonMsg::unpack(&msg.pack().unwrap()).unwrap(), msg);
        }

        let mut app = CharacterAppearance::default();
        app.boarding_on = BOARDING_ON_AIR_BALLOON;
        app.boarding_since = Some(100.0);
        app.boarding_until = Some(1000.0);
        app.vehicle_entity_id = "veh-balloon".into();
        let disp = PlayerDisplayMsg {
            entity_id: "player-1".into(),
            equip: Some("clothes_jobless".into()),
            head: None,
            appearance: app,
        };
        let dec_disp = PlayerDisplayMsg::unpack(&disp.pack().unwrap()).unwrap();
        assert_eq!(dec_disp.appearance.boarding_on, BOARDING_ON_AIR_BALLOON);
        assert_eq!(dec_disp.appearance.boarding_since, Some(100.0));
        assert_eq!(dec_disp.appearance.boarding_until, Some(1000.0));
        assert_eq!(dec_disp.appearance.vehicle_entity_id, "veh-balloon");
    }

    #[test]
    fn rejects_snappy_bomb_unbounded_arrays_and_deep_skip() {
        // 1. Snappy frame declaring 16 MiB uncompressed length must fail PayloadTooLarge without allocating 16 MiB
        let mut snappy_bomb = Vec::new();
        snappy_bomb.extend_from_slice(&0u64.to_le_bytes()); // time_ms
        snappy_bomb.extend_from_slice(&1u32.to_le_bytes()); // seq
        snappy_bomb.extend_from_slice(&0u32.to_le_bytes()); // reply_of
        snappy_bomb.extend_from_slice(&TC_MOVE.to_le_bytes());
        // Varint uncompressed length = 16_777_216 (0x80 0x80 0x80 0x08) + 1 literal byte
        let compressed = [0x80u8, 0x80, 0x80, 0x08, 0x00, 0x41];
        snappy_bomb.extend_from_slice(&(compressed.len() as u32).to_le_bytes());
        snappy_bomb.extend_from_slice(&compressed);
        assert!(matches!(
            try_decode_frame(&snappy_bomb),
            Err(WireError::PayloadTooLarge(16_777_216))
        ));

        // 2. Crafting huge 0xffffffff array header must return Err(Decode) without OOM abort
        // MoveMsg: array(2) [ str("e"), array32(0xffffffff) ]
        let huge_arr = [0x92u8, 0xa1, b'e', 0xdd, 0xff, 0xff, 0xff, 0xff];
        assert!(matches!(MoveMsg::unpack(&huge_arr), Err(WireError::Decode(_))));

        // 3. Deeply nested fixarrays in skipped field must fail cleanly instead of stack overflow
        let mut deep = vec![0x92u8, 0xa1, b'e'];
        deep.extend(std::iter::repeat_n(0x91u8, 64));
        deep.push(0x01);
        assert!(matches!(
            DumpItemsMsg::unpack(&deep).or_else(|e| Err(e)),
            Err(WireError::Decode(_))
        ));
        let mut skip_buf = std::iter::repeat_n(0x91u8, 64).collect::<Vec<u8>>();
        skip_buf.push(0xc0);
        let mut cur = Cursor::new(skip_buf.as_slice());
        assert!(matches!(skip_mp_value(&mut cur), Err(WireError::Decode(_))));

        // 4. Truncated fixed-width tag in skip_mp_value must return Err instead of advancing past EOF
        let trunc = [0xcf, 0x01, 0x02];
        let mut tcur = Cursor::new(trunc.as_slice());
        assert!(matches!(skip_mp_value(&mut tcur), Err(WireError::Decode(_))));
    }
}
