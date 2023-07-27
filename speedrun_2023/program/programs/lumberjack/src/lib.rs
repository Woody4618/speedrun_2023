use anchor_lang::prelude::*;
use gpl_session::{SessionError, SessionToken, session_auth_or, Session};

declare_id!("HsT4yX959Qh1vis8fEqoQdgrHEJuKvaWGtHoPcTjk4mJ");

#[error_code]
pub enum GameErrorCode {
    #[msg("Not enough energy")]
    NotEnoughEnergy,
    #[msg("Tile Already Occupied")]
    TileAlreadyOccupied,
    #[msg("Tile has no tree")]
    TileHasNoTree,
    #[msg("Wrong Authority")]
    WrongAuthority,
}

const TIME_TO_REFILL_ENERGY: i64 = 60;
const MAX_ENERGY: u64 = 10;
const BOARD_SIZE_X: usize = 10;
const BOARD_SIZE_Y: usize = 10;

#[program]
pub mod lumberjack {
    use super::*;

    pub fn init_player(ctx: Context<InitPlayer>) -> Result<()> {
        ctx.accounts.player.energy = MAX_ENERGY;
        ctx.accounts.player.last_login = Clock::get()?.unix_timestamp;
        ctx.accounts.player.authority = ctx.accounts.signer.key();
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn chop_tree(mut ctx: Context<BoardAction>, x: u8, y: u8) -> Result<()> {
        let account = &mut ctx.accounts;
        update_energy(account)?;

        if ctx.accounts.player.energy == 0 {
            return err!(GameErrorCode::NotEnoughEnergy);
        }

        if ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_type != 0 {
            return err!(GameErrorCode::TileHasNoTree);
        }
        
        ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_type = 1;

        ctx.accounts.board.load_mut()?.wood = ctx.accounts.board.load_mut()?.wood + 5;
        ctx.accounts.player.energy -= 1;
        msg!("You chopped a tree and got 1 wood. You have {} wood and {} energy left.",ctx.accounts.board.load_mut()?.wood, ctx.accounts.player.energy);
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn build(mut ctx: Context<BoardAction>, x :u8, y :u8, building_type :u8) -> Result<()> {
        let account = &mut ctx.accounts;
        update_energy(account)?;

        if ctx.accounts.player.energy == 0 {
            return err!(GameErrorCode::NotEnoughEnergy);
        }

        if ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_type != 0 {
            return err!(GameErrorCode::TileAlreadyOccupied);
        }

        // TODO: add build costs 
        ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_type = building_type;
        ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_start_collect_time = Clock::get()?.unix_timestamp;

        ctx.accounts.player.energy = ctx.accounts.player.energy - 1;
        msg!("You built a building. You have and {} energy left.", ctx.accounts.player.energy);
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn upgrade(mut ctx: Context<BoardAction>, x :u8, y :u8) -> Result<()> {
        let account = &mut ctx.accounts;
        update_energy(account)?;

        if ctx.accounts.player.energy == 0 {
            return err!(GameErrorCode::NotEnoughEnergy);
        }
        // TODO: add upgrade costs 
        ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_level = ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_level + 1;

        ctx.accounts.player.energy = ctx.accounts.player.energy - 1;
        msg!("You chopped a tree and got 1 wood. You have {} wood and {} energy left.", ctx.accounts.board.load_mut()?.wood, ctx.accounts.player.energy);
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn collect(mut ctx: Context<BoardAction>, x :u8, y :u8) -> Result<()> {
        let account = &mut ctx.accounts;
        update_energy(account)?;

        if ctx.accounts.player.energy == 0 {
            return err!(GameErrorCode::NotEnoughEnergy);
        }
        ctx.accounts.board.load_mut()?.board[x as usize][y as usize].building_start_collect_time = Clock::get()?.unix_timestamp;

        ctx.accounts.board.load_mut()?.wood = ctx.accounts.board.load_mut()?.wood + 5;
        ctx.accounts.player.energy = ctx.accounts.player.energy - 1;
        msg!("You collected from building. You have {} wood and {} energy left.", ctx.accounts.board.load_mut()?.wood, ctx.accounts.player.energy);
        Ok(())
    }

    pub fn update(mut ctx: Context<BoardAction>) -> Result<()> {
        let account = &mut ctx.accounts;
        update_energy(account)?;
        msg!("Updated energy. You have {} wood and {} energy left.", ctx.accounts.board.load_mut()?.wood, ctx.accounts.player.energy);
        Ok(())
    }
}

pub fn update_energy(ctx: &mut BoardAction) -> Result<()> {
    let mut time_passed: i64 = &Clock::get()?.unix_timestamp - &ctx.player.last_login;
    let mut time_spent: i64 = 0;
    while time_passed > TIME_TO_REFILL_ENERGY {
        ctx.player.energy = ctx.player.energy + 1;
        time_passed -= TIME_TO_REFILL_ENERGY;
        time_spent += TIME_TO_REFILL_ENERGY;
        if ctx.player.energy >= MAX_ENERGY {
            break;
        }
    }

    if ctx.player.energy >= MAX_ENERGY {
        ctx.player.last_login = Clock::get()?.unix_timestamp;
    } else {
        ctx.player.last_login += time_spent;
    }

    Ok(())
}

#[derive(Accounts)]
pub struct InitPlayer <'info> {
    #[account( 
        init,
        payer = signer,
        space = 1000,
        seeds = [b"player1".as_ref(), signer.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        init_if_needed,
        space = 10024,
        seeds = [b"board".as_ref()],
        payer = signer,
        bump,
    )]
    pub board: AccountLoader<'info, BoardAccount>,
    #[account(mut)]
    pub signer: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[account]
pub struct PlayerData {
    pub authority: Pubkey,
    pub avatar: Pubkey,
    pub name: String,
    pub level: u8,
    pub xp: u64,    
    pub energy: u64,
    pub last_login: i64
}

#[account(zero_copy(unsafe))]
#[repr(C)]
#[derive(Default)]
pub struct BoardAccount {
    board: [[Tile; BOARD_SIZE_X]; BOARD_SIZE_Y],
    action_id: u64,
    wood: u64, // Global resources, let see how it goes :D 
    stone: u64, // Global resources, let see how it goes :D 
    damm_level: u64, // Global building level of the mein goal
}

#[zero_copy(unsafe)]
#[repr(C)]
#[derive(Default)]
pub struct Tile {
    building_type: u8,
    building_level: u8,
    building_owner: Pubkey, // Could maybe be the avatar of the player building it? :thinking:
    building_start_time: i64,
    building_start_upgrade_time: i64,
    building_start_collect_time: i64,
}

#[derive(Accounts, Session)]
pub struct BoardAction <'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,
    #[account( 
        mut,
        seeds = [b"board".as_ref()],
        bump,
    )]
    pub board: AccountLoader<'info, BoardAccount>,
    #[account( 
        mut,
        seeds = [b"player1".as_ref(), player.authority.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account(mut)]
    pub signer: Signer<'info>,
}

