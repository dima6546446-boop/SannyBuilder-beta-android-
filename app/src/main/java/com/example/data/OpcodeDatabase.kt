package com.example.data

data class Opcode(
    val code: String,
    val syntax: String,
    val description: String,
    val category: String
)

object OpcodeDatabase {
    val categories = listOf("All", "Flow", "Player", "Vehicles", "Weapons", "Actor", "World")

    val opcodes = listOf(
        // Flow Control
        Opcode(
            "0001",
            "wait %1d ms",
            "Pauses script execution for specified milliseconds.",
            "Flow"
        ),
        Opcode(
            "0002",
            "jump @label",
            "Unconditional jump to a label.",
            "Flow"
        ),
        Opcode(
            "00D6",
            "if",
            "Begins a conditional statement structure.",
            "Flow"
        ),
        Opcode(
            "004D",
            "jump_if_false @label",
            "Jumps to the label if the preceding condition is false.",
            "Flow"
        ),
        Opcode(
            "03A4",
            "thread '%1s'",
            "Names the current execution thread (must be first command).",
            "Flow"
        ),
        Opcode(
            "004E",
            "end_thread",
            "Terminates execution of the current thread.",
            "Flow"
        ),

        // Player
        Opcode(
            "00DF",
            "actor %1d driving",
            "Checks if the actor is driving any vehicle.",
            "Player"
        ),
        Opcode(
            "00E1",
            "player %1d pressed_key %2d",
            "Checks if player has pressed a specific key ID.",
            "Player"
        ),
        Opcode(
            "010A",
            "player %1d money_to %2d",
            "Sets the amount of money for the specified player.",
            "Player"
        ),
        Opcode(
            "01B2",
            "give_actor %1d weapon %2d ammo %3d",
            "Gives a specific weapon and ammo amount to an actor.",
            "Weapons"
        ),
        Opcode(
            "01B4",
            "set_player %1d can_move %2d",
            "Enables (1) or disables (0) player movement.",
            "Player"
        ),
        Opcode(
            "053E",
            "set_player %1d inf_health %2d",
            "Enables (1) or disables (0) infinite health for a player.",
            "Player"
        ),

        // Vehicles
        Opcode(
            "0247",
            "load_model #%1s",
            "Loads a model (vehicle/weapon/skin) into memory.",
            "Vehicles"
        ),
        Opcode(
            "0248",
            "model #%1s available",
            "Checks if loaded model is ready to be spawned.",
            "Vehicles"
        ),
        Opcode(
            "0249",
            "release_model #%1s",
            "Unloads model from memory to free space.",
            "Vehicles"
        ),
        Opcode(
            "032B",
            "%1d = create_car #%2s at %3d %4d %5d",
            "Spawns a vehicle of model at coordinates and stores reference.",
            "Vehicles"
        ),
        Opcode(
            "0175",
            "set_car %1d z_angle_to %2d",
            "Sets the rotation (Z-axis angle) of a vehicle.",
            "Vehicles"
        ),
        Opcode(
            "020A",
            "set_car %1d health_to %2d",
            "Sets the body health of a vehicle (1000 is default/max).",
            "Vehicles"
        ),

        // Actor
        Opcode(
            "0223",
            "set_actor %1d health_to %2d",
            "Sets health points for an actor.",
            "Actor"
        ),
        Opcode(
            "035F",
            "set_actor %1d armor_to %2d",
            "Sets armor points for an actor.",
            "Actor"
        ),
        Opcode(
            "04C4",
            "store_coords_to %1d %2d %3d from_actor %4d with_offset %5d %6d %7d",
            "Gets coordinates of actor with relative offset.",
            "Actor"
        ),
        Opcode(
            "02AB",
            "set_actor %1d immunities fire %2d water %3d damage %4d staff %5d explosion %6d",
            "Sets bullet, fire, and explosion immunities for an actor.",
            "Actor"
        ),

        // Weapons
        Opcode(
            "01B9",
            "set_actor %1d armed_weapon_to %2d",
            "Forces actor to equip specific weapon from inventory.",
            "Weapons"
        ),
        Opcode(
            "0224",
            "set_actor %1d health_to_of_actor %2d",
            "Copies health from one actor to another.",
            "Actor"
        ),

        // World
        Opcode(
            "00BF",
            "show_text_styled '%1s' time %2d style %3d",
            "Displays high priority mission styled text on screen.",
            "World"
        ),
        Opcode(
            "00BC",
            "show_text_highpriority '%1s' time %2d flag %3d",
            "Displays a notification text on top left of screen.",
            "World"
        ),
        Opcode(
            "0169",
            "set_time_of_day %1d %2d",
            "Changes game clock to specific hours and minutes.",
            "World"
        ),
        Opcode(
            "02F0",
            "enable_screen_fading_with_color %1d %2d %3d speed %4d",
            "Fades game screen to specified RGB color at custom speed.",
            "World"
        )
    )
}
