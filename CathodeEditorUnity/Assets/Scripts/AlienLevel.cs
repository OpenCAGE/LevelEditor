using CATHODE;
using CATHODE.LEGACY;
using System.IO;

namespace CathodeLib
{
    public class AlienLevel
    {
        public static alien_level Load(string LEVEL_NAME, string ENV_PATH)
        {
            alien_level Result = new alien_level();
            string levelPath = ENV_PATH + "/PRODUCTION/" + LEVEL_NAME;
            /*** WORLD ***/

            Result.ModelsMVR = new Movers(levelPath + "/WORLD/MODELS.MVR");
            Result.CommandsPAK = new Commands(levelPath + "/WORLD/COMMANDS.PAK");
            Result.RenderableREDS = new RenderableElements(levelPath + "/WORLD/REDS.BIN");
            Result.ResourcesBIN = new Resources(levelPath + "/WORLD/RESOURCES.BIN");
            Result.PhysicsMap = new PhysicsMaps(levelPath + "/WORLD/PHYSICS.MAP");
            Result.EnvironmentMap = new EnvironmentMaps(levelPath + "/WORLD/ENVIRONMENTMAP.BIN");
            Result.CollisionMap = new CollisionMaps(levelPath + "/WORLD/COLLISION.MAP");
            Result.EnvironmentAnimation = new EnvironmentAnimations(levelPath + "/WORLD/ENVIRONMENT_ANIMATION.DAT");
            //ALPHALIGHT_LEVEL.BIN
            //BEHAVIOR_TREE.DB
            //CHARACTERACCESSORYSETS.BIN
            //COLLISION.BIN
            //COLLISION.HKX
            //COLLISION.HKX64
            //CUTSCENE_DIRECTOR_DATA.BIN
            //EXCLUSIVE_MASTER_RESOURCE_INDICES
            //LEVEL.STR
            //LIGHTS.BIN
            //MATERIAL_MAPPINGS.PAK
            //MORPH_TARGET_DB.BIN
            //OCCLUDER_TRIANGLE_BVH.BIN
            //PATH_BARRIER_RESOURCES
            //PHYSICS.HKX
            //PHYSICS.HKX64
            //RADIOSITY_COLLISION_MAPPING.BIN
            //SNDNODENETWORK.DAT
            //SOUNDBANKDATA.DAT
            //SOUNDDIALOGUELOOKUPS.DAT
            //SOUNDENVIRONMENTDATA.DAT
            //SOUNDEVENTDATA.DAT
            //SOUNDFLASHMODELS.DAT
            //SOUNDLOADZONES.DAT
            //STATE_x/ASSAULT_POSITIONS
            //STATE_x/COVER
            //STATE_x/CRAWL_SPACE_SPOTTING_POSITIONS
            //STATE_x/NAV_MESH
            //STATE_x/SPOTTING_POSITIONS
            //STATE_x/TRAVERSAL
            /*** RENDERABLE ***/

            Result.ModelsCST = File.ReadAllBytes(levelPath + "/RENDERABLE/LEVEL_MODELS.CST");
            Result.ModelsMTL = new Materials(levelPath + "/RENDERABLE/LEVEL_MODELS.MTL");
            Result.ModelsPAK = new Models(levelPath + "/RENDERABLE/LEVEL_MODELS.PAK");
            Result.ShadersPAK = new ShadersPAK(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11.PAK");
            //Result.ShadersBIN = CATHODE.Shaders.ShadersBIN.Load(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_BIN.PAK");
            Result.ShadersIDXRemap = new IDXRemap(levelPath + "/RENDERABLE/LEVEL_SHADERS_DX11_IDX_REMAP.PAK");
            Result.LevelTextures = new Textures(levelPath + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK");
            //LEVEL_TEXTURES.DX11.PAK
            //RADIOSITY_INSTANCE_MAP.TXT
            //RADIOSITY_RUNTIME.BIN
            //DAMAGE/DAMAGE_MAPPING_INFO.BIN
            //GALAXY/GALAXY.DEFINITION_BIN
            //GALAXY/GALAXY.ITEMS_BIN
            return Result;
        }
    }
}
public class alien_level
{
    public Movers ModelsMVR;
    public Commands CommandsPAK;
    public RenderableElements RenderableREDS;
    public Resources ResourcesBIN;
    public PhysicsMaps PhysicsMap;
    public EnvironmentMaps EnvironmentMap;
    public CollisionMaps CollisionMap;
    public EnvironmentAnimations EnvironmentAnimation;
    public byte[] ModelsCST;
    public Materials ModelsMTL;
    public Models ModelsPAK;
    public Textures LevelTextures;
    public ShadersPAK ShadersPAK;
    public alien_shader_bin_pak ShadersBIN;
    public IDXRemap ShadersIDXRemap;
};