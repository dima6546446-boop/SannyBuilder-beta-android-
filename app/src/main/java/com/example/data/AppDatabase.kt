package com.example.data

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.sqlite.db.SupportSQLiteDatabase
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

@Database(entities = [ScriptProject::class], version = 1, exportSchema = false)
abstract class AppDatabase : RoomDatabase() {
    abstract fun scriptProjectDao(): ScriptProjectDao

    companion object {
        @Volatile
        private var INSTANCE: AppDatabase? = null

        fun getDatabase(context: Context, scope: CoroutineScope): AppDatabase {
            return INSTANCE ?: synchronized(this) {
                val instance = Room.databaseBuilder(
                    context.applicationContext,
                    AppDatabase::class.java,
                    "sunnybuilder_database"
                )
                .addCallback(AppDatabaseCallback(scope))
                .build()
                INSTANCE = instance
                instance
            }
        }
    }

    private class AppDatabaseCallback(
        private val scope: CoroutineScope
    ) : RoomDatabase.Callback() {
        override fun onCreate(db: SupportSQLiteDatabase) {
            super.onCreate(db)
            INSTANCE?.let { database ->
                scope.launch(Dispatchers.IO) {
                    val dao = database.scriptProjectDao()
                    
                    // Pre-populate with classic GTA CLEO script templates
                    dao.insertProject(
                        ScriptProject(
                            title = "Spawn Hydra Jet",
                            code = """// === GTA SA Spawn Hydra Jet ===
// Press H (Horn) to spawn a Hydra at player position

{${'$'}CLEO .cs}

:SPAWN_HYDRA_START
thread 'SPAWNHYDRA'

:SPAWN_HYDRA_LOOP
wait 100 ms
if
  Player.Defined(${'$'}PLAYER_CHAR)
jf @SPAWN_HYDRA_LOOP
if
  Actor.Driving(${'$'}PLAYER_ACTOR)
jf @CHECK_KEY

// If driving, do nothing
jump @SPAWN_HYDRA_LOOP

:CHECK_KEY
if
  00E1: player 0 pressed_key 15 // H (Horn) / Horn key
jf @SPAWN_HYDRA_LOOP

// Spawn Hydra Jet
0247: load_model #HYDRA
:LOAD_MODEL_LOOP
wait 0 ms
if
  0248: model #HYDRA available
jf @LOAD_MODEL_LOOP

04C4: store_coords_to ${'$'}X ${'$'}Y ${'$'}Z from_actor ${'$'}PLAYER_ACTOR with_offset 0.0 5.0 1.0
032B: ${'$'}CAR = create_car #HYDRA at ${'$'}X ${'$'}Y ${'$'}Z
0175: set_car ${'$'}CAR z_angle_to 180.0
0249: release_model #HYDRA

// Flash text on screen
00BC: show_text_highpriority "HYDRA SPAWNED" time 2000 flag 1
wait 2000 ms
jump @SPAWN_HYDRA_LOOP
""",
                            lastModified = System.currentTimeMillis() - 1000
                        )
                    )

                    dao.insertProject(
                        ScriptProject(
                            title = "Infinite Health & Armor",
                            code = """// === Infinite Health & Armor ===
// Keeps your health and armor at maximum continuously

{${'$'}CLEO .cs}

:GOD_MODE_START
thread 'GODMODE'

:GOD_MODE_LOOP
wait 200 ms
if
  Player.Defined(${'$'}PLAYER_CHAR)
jf @GOD_MODE_LOOP

// Set health and armor to 250
0223: set_actor ${'$'}PLAYER_ACTOR health_to 250
035F: set_actor ${'$'}PLAYER_ACTOR armor_to 250

// Make player immune to fire, explosions, bullets, collision
02AB: set_actor ${'$'}PLAYER_ACTOR immunities fire 1 water 0 damage 1 staff 1 explosion 1

jump @GOD_MODE_LOOP
""",
                            lastModified = System.currentTimeMillis() - 5000
                        )
                    )

                    dao.insertProject(
                        ScriptProject(
                            title = "Super Jump Script",
                            code = """// === Super Jump CLEO ===
// Press JUMP + SPRINT keys to leap high in the air

{${'$'}CLEO .cs}

:SUPER_JUMP_START
thread 'SUPJUMP'

:SUPER_JUMP_LOOP
wait 50 ms
if
  Player.Defined(${'$'}PLAYER_CHAR)
jf @SUPER_JUMP_LOOP

if
  00E1: player 0 pressed_key 14 // SPRINT key
jf @SUPER_JUMP_LOOP

if
  00E1: player 0 pressed_key 16 // JUMP key
jf @SUPER_JUMP_LOOP

if
  Actor.Driving(${'$'}PLAYER_ACTOR)
jf @DO_JUMP

// Can't jump if in vehicle
jump @SUPER_JUMP_LOOP

:DO_JUMP
04C4: store_coords_to ${'$'}X ${'$'}Y ${'$'}Z from_actor ${'$'}PLAYER_ACTOR with_offset 0.0 0.0 0.0
02E3: apply_force_to_actor ${'$'}PLAYER_ACTOR delta_coords 0.0 0.0 15.0 delta_rotation 0.0 0.0 0.0

// Flash screen effect
03F0: enable_screen_fading_with_color 255 255 255 speed 100
wait 100 ms
03F1: enable_screen_fading_with_color 0 0 0 speed 100

wait 1500 ms // Cooldown
jump @SUPER_JUMP_LOOP
""",
                            lastModified = System.currentTimeMillis() - 10000
                        )
                    )
                }
            }
        }
    }
}
