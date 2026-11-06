// Only built in Shared/, so BossOptions builds without the game. The mod itself uses the game's own enum.
// The names must match the game's BossType: UnitTests/BossTypeTests checks them against web/src/data/game.json, which
// the in-game WebDataTest writes. When the game adds a boss, update game.json first, then this.
namespace Il2CppAssets.Scripts.Data.Boss;

public enum BossType
{
    Bloonarius,
    Lych,
    Vortex,
    Dreadbloon,
    Phayze,
    Blastapopoulos,
    Diamondback
}
