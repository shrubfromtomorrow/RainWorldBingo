
using Menu;
namespace BingoMode
{
    public class BingoEnums
    {
        public static Slider.SliderID CustomizerSlider;
        public static Slider.SliderID MultiplayerSlider;
        public static Slider.SliderID RandomizerSlider;

        public static MenuScene.SceneID MainMenu_Bingo;
        public static MenuScene.SceneID WatcherExpeditionBackground;
        public static MenuScene.SceneID SofanthielExpeditionBackground;

        public static SlideShow.SlideShowID Sluhvengers;
        public static SlideShow.SlideShowID MenuTest;

        public static SoundID BINGO_FINAL_BONG;

        public static ProcessManager.ProcessID BingoCredits;

        public static int TeamCount = 11;

        public static void Register()
        {
            CustomizerSlider = new("CustomizerSlider", true);
            MultiplayerSlider = new("MultiplayerSlider", true);
            RandomizerSlider = new("RandomizerSlider", true);

            BINGO_FINAL_BONG = new("BINGO_FINAL_BONG", true);

            BingoCredits = new("BingoCredits", true);

            MainMenu_Bingo = new MenuScene.SceneID("main menu - bingo", true);

            WatcherExpeditionBackground = new MenuScene.SceneID("watcher expedition background - bingo", true);
            SofanthielExpeditionBackground = new MenuScene.SceneID("sofanthiel expedition background - bingo", true);

            Sluhvengers = new SlideShow.SlideShowID("Sluhvengers", true);
            MenuTest = new SlideShow.SlideShowID("MenuTest", true);

            LandscapeType.RegisterValues();
            SluhvengersScenes.RegisterValues();
        }

        public class SluhvengersScenes
        {
            public static MenuScene.SceneID sluhvengers_1_surmonk;
            public static MenuScene.SceneID sluhvengers_2_hunter;
            public static MenuScene.SceneID sluhvengers_3_saint;
            public static MenuScene.SceneID sluhvengers_4_gour;
            public static MenuScene.SceneID sluhvengers_5_arti;
            public static MenuScene.SceneID sluhvengers_6_sm;
            public static MenuScene.SceneID sluhvengers_7_riv;
            public static MenuScene.SceneID sluhvengers_8_eyes;
            public static MenuScene.SceneID sluhvengers_9_sluhvengers;

            public static void RegisterValues()
            {
                var fields = typeof(SluhvengersScenes).GetFields(
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(MenuScene.SceneID) &&
                        field.Name.StartsWith("sluhvengers_"))
                    {
                        string name = field.Name;
                        var instance = new MenuScene.SceneID(name, true);
                        field.SetValue(null, instance);
                    }
                }
            }

            public static void UnregisterValues()
            {
                var fields = typeof(SluhvengersScenes).GetFields(
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(MenuScene.SceneID) &&
                        field.Name.StartsWith("sluhvengers_"))
                    {
                        var id = field.GetValue(null) as MenuScene.SceneID;
                        if (id != null)
                        {
                            id.Unregister();
                            field.SetValue(null, null);
                        }
                    }
                }
            }
        }

        public class LandscapeType
        {
            public static MenuScene.SceneID Landscape_WRFA;
            public static MenuScene.SceneID Landscape_WARB;
            public static MenuScene.SceneID Landscape_WBLA;
            public static MenuScene.SceneID Landscape_WSKC;
            public static MenuScene.SceneID Landscape_WTDA;
            public static MenuScene.SceneID Landscape_WARF;
            public static MenuScene.SceneID Landscape_WARA;
            public static MenuScene.SceneID Landscape_WARC;
            public static MenuScene.SceneID Landscape_WARD;
            public static MenuScene.SceneID Landscape_WARE;
            public static MenuScene.SceneID Landscape_WARG;
            public static MenuScene.SceneID Landscape_WAUA;
            public static MenuScene.SceneID Landscape_WDSR;
            public static MenuScene.SceneID Landscape_WGWR;
            public static MenuScene.SceneID Landscape_WHIR;
            public static MenuScene.SceneID Landscape_WMPA;
            public static MenuScene.SceneID Landscape_WORA;
            public static MenuScene.SceneID Landscape_WPGA;
            public static MenuScene.SceneID Landscape_WPTA;
            public static MenuScene.SceneID Landscape_WRFB;
            public static MenuScene.SceneID Landscape_WRRA;
            public static MenuScene.SceneID Landscape_WRSA;
            public static MenuScene.SceneID Landscape_WSKA;
            public static MenuScene.SceneID Landscape_WSKB;
            public static MenuScene.SceneID Landscape_WSKD;
            public static MenuScene.SceneID Landscape_WSSR;
            public static MenuScene.SceneID Landscape_WSUR;
            public static MenuScene.SceneID Landscape_WTDB;
            public static MenuScene.SceneID Landscape_WVWA;
            public static MenuScene.SceneID Landscape_WVWB;

            public static void RegisterValues()
            {
                var fields = typeof(LandscapeType).GetFields(
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(MenuScene.SceneID) &&
                        field.Name.StartsWith("Landscape_"))
                    {
                        string name = field.Name;
                        var instance = new MenuScene.SceneID(name, true);
                        field.SetValue(null, instance);
                    }
                }
            }

            public static void UnregisterValues()
            {
                var fields = typeof(LandscapeType).GetFields(
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(MenuScene.SceneID) &&
                        field.Name.StartsWith("Landscape_"))
                    {
                        var id = field.GetValue(null) as MenuScene.SceneID;
                        if (id != null)
                        {
                            id.Unregister();
                            field.SetValue(null, null);
                        }
                    }
                }
            }
        }
    }
}
