
namespace BingoMode
{
    public class BingoEnums
    {
        public static Menu.Slider.SliderID CustomizerSlider;
        public static Menu.Slider.SliderID MultiplayerSlider;
        public static Menu.Slider.SliderID RandomizerSlider;

        public static Menu.MenuScene.SceneID MainMenu_Bingo;
        public static Menu.MenuScene.SceneID WatcherExpeditionBackground;


        public static SoundID BINGO_FINAL_BONG;

        public static ProcessManager.ProcessID BingoCredits;

        public static void Register()
        {
            CustomizerSlider = new("CustomizerSlider", true);
            MultiplayerSlider = new("MultiplayerSlider", true);
            RandomizerSlider = new("RandomizerSlider", true);

            BINGO_FINAL_BONG = new("BINGO_FINAL_BONG", true);

            BingoCredits = new("BingoCredits", true);

            MainMenu_Bingo = new Menu.MenuScene.SceneID("main menu - bingo", true);

            WatcherExpeditionBackground = new Menu.MenuScene.SceneID("watcher expedition background - bingo", true);
        }

        public class LandscapeType
        {
            public static Menu.MenuScene.SceneID Landscape_WRFA;
            public static Menu.MenuScene.SceneID Landscape_WARB;
            public static Menu.MenuScene.SceneID Landscape_WBLA;
            public static Menu.MenuScene.SceneID Landscape_WSKC;
            public static Menu.MenuScene.SceneID Landscape_WTDA;
            public static Menu.MenuScene.SceneID Landscape_WARF;
            public static Menu.MenuScene.SceneID Landscape_WARA;
            public static Menu.MenuScene.SceneID Landscape_WARC;
            public static Menu.MenuScene.SceneID Landscape_WARD;
            public static Menu.MenuScene.SceneID Landscape_WARE;
            public static Menu.MenuScene.SceneID Landscape_WARG;
            public static Menu.MenuScene.SceneID Landscape_WAUA;
            public static Menu.MenuScene.SceneID Landscape_WDSR;
            public static Menu.MenuScene.SceneID Landscape_WGWR;
            public static Menu.MenuScene.SceneID Landscape_WHIR;
            public static Menu.MenuScene.SceneID Landscape_WMPA;
            public static Menu.MenuScene.SceneID Landscape_WORA;
            public static Menu.MenuScene.SceneID Landscape_WPGA;
            public static Menu.MenuScene.SceneID Landscape_WPTA;
            public static Menu.MenuScene.SceneID Landscape_WRFB;
            public static Menu.MenuScene.SceneID Landscape_WRRA;
            public static Menu.MenuScene.SceneID Landscape_WRSA;
            public static Menu.MenuScene.SceneID Landscape_WSKA;
            public static Menu.MenuScene.SceneID Landscape_WSKB;
            public static Menu.MenuScene.SceneID Landscape_WSKD;
            public static Menu.MenuScene.SceneID Landscape_WSSR;
            public static Menu.MenuScene.SceneID Landscape_WSUR;
            public static Menu.MenuScene.SceneID Landscape_WTDB;
            public static Menu.MenuScene.SceneID Landscape_WVWA;
            public static Menu.MenuScene.SceneID Landscape_WVWB;

            public static void RegisterValues()
            {
                var fields = typeof(LandscapeType).GetFields(
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Menu.MenuScene.SceneID) &&
                        field.Name.StartsWith("Landscape_"))
                    {
                        string name = field.Name;
                        var instance = new Menu.MenuScene.SceneID(name, true);
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
                    if (field.FieldType == typeof(Menu.MenuScene.SceneID) &&
                        field.Name.StartsWith("Landscape_"))
                    {
                        var id = field.GetValue(null) as Menu.MenuScene.SceneID;
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
