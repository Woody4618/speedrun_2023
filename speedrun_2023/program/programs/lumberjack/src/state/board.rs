use anchor_lang::prelude::*;
use solana_program::pubkey;

use crate::GameErrorCode;
use crate::BOARD_SIZE_X;
use crate::BOARD_SIZE_Y;

const BUILDING_TYPE_TREE: u8 = 0;
const BUILDING_TYPE_EMPTY: u8 = 1;
const BUILDING_TYPE_SAWMILL: u8 = 2;
const BUILDING_TYPE_MINE: u8 = 3;

const ACTION_TYPE_CHOP: u8 = 0;
const ACTION_TYPE_BUILD: u8 = 1;
const ACTION_TYPE_UPGRADE: u8 = 2;
const ACTION_TYPE_COLLECT: u8 = 3;

impl BoardAccount {
    pub fn chop_tree(
        &mut self,
        x: u8,
        y: u8,
        player: Pubkey,
        avatar: Pubkey,
        game_actions: &mut GameActionHistory,
    ) -> Result<()> {
        //let tile = self.board[x][y];

        if self.data[x as usize][y as usize].building_type != BUILDING_TYPE_TREE {
            return err!(GameErrorCode::TileHasNoTree);
        }

        self.data[x as usize][y as usize].building_type = BUILDING_TYPE_EMPTY;
        self.wood += 5;

        let new_game_action = GameAction {
            action_id: self.action_id,
            action_type: ACTION_TYPE_CHOP,
            x,
            y,
            player: player.key(),
            avatar: avatar.key(),
            tile: self.data[x as usize][y as usize],
        };

        self.add_new_game_action(game_actions, new_game_action);
        Ok(())
    }

    pub fn build(
        &mut self,
        x: u8,
        y: u8,
        building_type: u8,
        player: Pubkey,
        avatar: Pubkey,
        game_actions: &mut GameActionHistory,
    ) -> Result<()> {
        if self.data[x as usize][y as usize].building_type != BUILDING_TYPE_EMPTY {
            return err!(GameErrorCode::TileAlreadyOccupied);
        }

        // TODO: add build costs
        self.data[x as usize][y as usize].building_type = building_type;
        self.data[x as usize][y as usize].building_start_collect_time =
            Clock::get()?.unix_timestamp;

        let new_game_action = GameAction {
            action_id: self.action_id,
            action_type: ACTION_TYPE_BUILD,
            x,
            y,
            player: player.key(),
            avatar: avatar.key(),
            tile: self.data[x as usize][y as usize],
        };
        self.add_new_game_action(game_actions, new_game_action);

        Ok(())
    }

    pub fn upgrade(
        &mut self,
        x: u8,
        y: u8,
        player: Pubkey,
        avatar: Pubkey,
        game_actions: &mut GameActionHistory,
    ) -> Result<()> {
        if self.data[x as usize][y as usize].building_type != BUILDING_TYPE_SAWMILL
            && self.data[x as usize][y as usize].building_type != BUILDING_TYPE_MINE
        {
            return err!(GameErrorCode::TileCantBeUpgraded);
        }

        // TODO: add upgrade costs
        self.data[x as usize][y as usize].building_level += 1;

        let new_game_action = GameAction {
            action_id: self.action_id,
            action_type: ACTION_TYPE_UPGRADE,
            x,
            y,
            player: player.key(),
            avatar: avatar.key(),
            tile: self.data[x as usize][y as usize],
        };
        self.add_new_game_action(game_actions, new_game_action);

        Ok(())
    }

    pub fn collect(
        &mut self,
        x: u8,
        y: u8,
        player: Pubkey,
        avatar: Pubkey,
        game_actions: &mut GameActionHistory,
    ) -> Result<()> {
        if self.data[x as usize][y as usize].building_type != BUILDING_TYPE_SAWMILL
            && self.data[x as usize][y as usize].building_type != BUILDING_TYPE_MINE
        {
            return err!(GameErrorCode::TileAlreadyOccupied);
        }

        if (Clock::get()?.unix_timestamp
            - self.data[x as usize][y as usize].building_start_collect_time)
            < 60
        {
            return err!(GameErrorCode::TileCantBeCollected);
        }

        self.data[x as usize][y as usize].building_start_collect_time =
            Clock::get()?.unix_timestamp;

        if (self.data[x as usize][y as usize].building_type == BUILDING_TYPE_SAWMILL) {
            self.wood += 5;
        } else {
            self.stone += 5;
        }

        let new_game_action = GameAction {
            action_id: self.action_id,
            action_type: ACTION_TYPE_COLLECT,
            x,
            y,
            player: player.key(),
            avatar: avatar.key(),
            tile: self.data[x as usize][y as usize],
        };
        self.add_new_game_action(game_actions, new_game_action);

        Ok(())
    }

    fn add_new_game_action(
        &mut self,
        game_actions: &mut GameActionHistory,
        game_action: GameAction,
    ) {
        {
            let option_add = self.action_id.checked_add(1);
            match option_add {
                Some(val) => {
                    self.action_id = val;
                }
                None => {
                    self.action_id = 0;
                }
            }
        }
        game_actions.game_actions[game_actions.action_index as usize] = game_action;
        game_actions.action_index = (game_actions.action_index + 1) % 30;
    }
}

#[account(zero_copy(unsafe))]
#[repr(packed)]
#[derive(Default)]
pub struct BoardAccount {
    pub data: [[TileData; BOARD_SIZE_X]; BOARD_SIZE_Y],
    pub action_id: u64,
    pub wood: u64,       // Global resources, let see how it goes :D
    pub stone: u64,      // Global resources, let see how it goes :D
    pub damm_level: u64, // Global building level of the mein goal
}

#[zero_copy(unsafe)]
#[repr(packed)]
#[derive(Default)]
pub struct TileData {
    pub building_type: u8,
    pub building_level: u8,
    pub building_owner: Pubkey, // Could maybe be the avatar of the player building it? :thinking:
    pub building_start_time: i64,
    pub building_start_upgrade_time: i64,
    pub building_start_collect_time: i64,
}

#[account(zero_copy(unsafe))]
#[repr(packed)]
#[derive(Default)]
pub struct GameActionHistory {
    id_counter: u64,
    action_index: u64,
    game_actions: [GameAction; 30],
}

#[zero_copy(unsafe)]
#[repr(packed)]
#[derive(Default)]
pub struct GameAction {
    action_id: u64,  // 1
    action_type: u8, // 1
    x: u8,           // 1
    y: u8,           // 1
    tile: TileData,  // 32
    player: Pubkey,  // 32
    avatar: Pubkey,  // 32
}
