using UnityEditor;
using UnityEngine;

// Source of the per-view joint placement. No mirrored/rotated camera-view substitutes.
public static class HiveTyrantPoseAuthoring
{
    public const string Path = "Assets/_Project/Animations/Boss/HiveTyrant/HiveTyrant_PoseLibrary.asset";
    public static HiveTyrantPoseLibrary GetOrCreate()
    {
        var library = AssetDatabase.LoadAssetAtPath<HiveTyrantPoseLibrary>(Path);
        if (library != null) { UpgradeJoints(library); return library; } // Respect subsequent artist edits.
        library = ScriptableObject.CreateInstance<HiveTyrantPoseLibrary>();
        string[] directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
        Vector2[] scytheJ = { V(380,575), V(435,615), V(605,575), V(780,560), V(700,640), V(710,520), V(350,620), V(355,650) };
        Vector2[] scytheT = { V(330,840), V(475,855), V(690,825), V(920,690), V(830,740), V(805,585), V(230,725), V(280,850) };
        Vector2[] fistJ = { V(625,570), V(745,645), V(760,640), V(440,490), V(330,655), V(350,620), V(460,700), V(650,680) };
        Vector2[] fistT = { V(705,820), V(800,810), V(785,735), V(360,565), V(220,770), V(260,750), V(450,875), V(660,870) };
        Vector2[][] scythe = {
            P(370,520, 455,610, 425,740, 495,930, 490,1024, 160,1024, 100,740, 220,600),
            P(400,555, 510,595, 505,720, 665,875, 660,1024, 250,1024, 240,760, 300,630),
            P(565,525, 670,565, 690,700, 910,775, 930,980, 600,990, 455,820, 470,620),
            P(750,500, 875,540, 1024,600, 1024,865, 845,830, 800,680, 730,600),
            P(665,575, 760,590, 900,650, 960,850, 760,870, 730,730, 650,675),
            P(670,460, 790,470, 910,565, 930,690, 780,700, 710,610, 650,550),
            P(330,570, 420,620, 390,750, 290,900, 100,890, 90,650, 210,595),
            P(320,595, 425,650, 380,780, 400,990, 160,1024, 80,815, 160,685)
        };
        Vector2[][] fist = {
            P(610,520, 730,580, 830,720, 830,970, 550,980, 550,775, 650,670),
            P(695,570, 800,585, 925,740, 925,965, 680,965, 645,780, 710,705),
            P(720,590, 825,615, 880,710, 825,840, 690,805, 680,700),
            P(420,435, 535,460, 500,565, 415,650, 280,650, 260,540, 330,470),
            P(300,590, 390,650, 330,770, 290,900, 95,900, 100,720, 200,640),
            P(310,570, 420,625, 365,755, 330,900, 115,870, 110,700, 210,620),
            P(420,635, 540,680, 565,845, 545,1024, 260,1024, 270,825, 360,720),
            P(595,625, 730,660, 800,815, 800,1024, 510,1024, 510,825, 590,745)
        };
        for (int phase = 0; phase < 2; phase++) for (int d = 0; d < 8; d++)
        {
            string source = phase == 0 ? "Assets/_Project/Art/Boss/HiveTyrant/Idle/D/HiveTyrant_Idle_" + directions[d] + "_01.png" :
                "Assets/_Project/Art/Boss/HiveTyrant/Idle_Phase2 1/D/HiveTyrant_Idle_Phase2_" + directions[d] + "_01.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(source);
            if (sprite == null) throw new System.InvalidOperationException("Missing pose master: " + source);
            Vector2 offset = phase == 1 ? new Vector2(0, -20) : Vector2.zero;
            library.directions[phase * 8 + d] = new HiveTyrantDirectionalRig {
                source = sprite, scytheJoint = scytheJ[d] + offset, scytheTip = scytheT[d] + offset,
                fistJoint = fistJ[d] + offset, fistTip = fistT[d] + offset,
                scytheRegion = Shift(scythe[d], offset), fistRegion = Shift(fist[d], offset),
                chestPivot = V(510, 690), torsoAnchorY = 820, swingSign = d >= 3 && d <= 6 ? -1 : 1,
                wingHinge = V(d == 2 ? 570 : d == 6 ? 440 : 512, 460)
            };
        }
        AssetDatabase.CreateAsset(library, Path); UpgradeJoints(library); return library;
    }
    private static void UpgradeJoints(HiveTyrantPoseLibrary library)
    {
        if (library.authoringVersion >= 8) return;
        for (int phase = 0; phase < 2; phase++) for (int d = 0; d < 8; d++)
        {
            var rig = library.directions[phase * 8 + d];
            rig.jointAmplitude = d == 2 || d == 6 ? .6f : d == 3 || d == 5 ? .55f : d == 0 || d == 4 ? .8f : 1;
            if (d == 2) rig.scytheRegion = P(525,510, 645,535, 680,655, 920,750, 960,1015, 570,1024, 440,850, 430,675, 490,590);
            if (d == 0) rig.scytheRegion = P(335,520, 450,555, 455,670, 510,1024, 40,1024, 50,740, 170,610, 290,560);
            if (d == 6)
            {
                rig.scytheJoint = V(280,625); rig.scytheTip = V(240,800);
                rig.scytheRegion = P(270,580, 350,610, 340,680, 350,720, 355,825, 355,960, 20,960, 20,620, 210,600);
            }
            if (d == 5) rig.scytheRegion = P(615,420, 715,450, 825,560, 805,740, 750,725, 700,640, 650,600, 610,520);
            if (d == 7)
            {
                rig.fistJoint = V(535,590); rig.fistTip = V(565,820);
                rig.fistRegion = P(485,535, 600,565, 660,635, 700,765, 720,1024, 385,1024, 385,825, 480,730, 530,650);
                rig.scytheJoint = V(300,550); rig.scytheTip = V(240,750);
                rig.scytheRegion = P(265,485, 375,530, 350,600, 330,680, 390,900, 405,1024, 0,1024, 0,760, 110,580, 180,530);
            }
            if (d == 3 && phase == 1)
            {
                rig.scytheJoint = V(665,550); rig.scytheTip = V(845,780);
                rig.scytheRegion = P(650,515, 745,545, 800,595, 945,730, 970,970, 780,970, 750,790, 680,700, 645,625);
            }
        }
        // Trace each wrist against its actual view: rear calves are never part of an arm mask.
        for (int phase = 0; phase < 2; phase++)
        {
            var u = library.directions[phase * 8 + 4];
            u.scytheJoint = V(740,550); u.scytheTip = V(825,700);
            u.scytheRegion = P(695,510, 790,485, 950,540, 980,900, 710,925, 670,760, 675,635, 710,585);
            u.fistJoint = V(300,545); u.fistTip = V(215,710);
            u.fistRegion = P(265,480, 365,510, 365,580, 340,630, 330,860, 50,880, 50,610, 175,530);
            var ul = library.directions[phase * 8 + 5];
            ul.scytheJoint = V(650,520); ul.scytheTip = V(755,635);
            ul.scytheRegion = P(605,465, 710,470, 835,560, 895,775, 730,800, 695,665, 625,600, 610,545);
            ul.fistJoint = V(320,570); ul.fistTip = V(235,730);
            ul.fistRegion = P(270,515, 355,530, 375,610, 350,665, 335,725, 315,800, 300,950, 45,950, 45,650, 190,565);
            var l = library.directions[phase * 8 + 6];
            l.scytheJoint = V(260,665); l.scytheTip = V(190,760);
            l.scytheRegion = P(225,610, 315,625, 330,670, 300,725, 260,805, 240,890, 40,920, 40,630);
            l.fistJoint = V(395,700); l.fistTip = V(390,850);
            l.fistRegion = P(360,625, 460,625, 540,700, 595,860, 565,1024, 220,1024, 210,860, 290,770, 340,735);
            if (phase == 1)
            {
                l.scytheJoint = V(265,610); l.scytheTip = V(180,760);
                l.scytheRegion = P(215,555, 300,565, 330,620, 300,680, 290,800, 250,970, 20,985, 15,640);
                l.fistJoint = V(500,675); l.fistTip = V(525,840);
                l.fistRegion = P(450,615, 540,615, 635,720, 670,1024, 370,1024, 365,795, 430,730);
                var dl = library.directions[15];
                dl.fistJoint = V(585,665); dl.fistTip = V(555,835);
                dl.fistRegion = P(550,610, 650,625, 700,740, 720,1024, 395,1024, 390,810, 470,755, 550,710);
                dl.scytheJoint = V(240,620); dl.scytheTip = V(185,770);
                dl.scytheRegion = P(180,545, 275,560, 300,645, 325,720, 375,940, 20,980, 0,650, 105,570);
            }
        }
        for (int phase = 0; phase < 2; phase++)
        {
            int i = phase * 8;
            library.directions[i].scytheRegion = P(335,520,450,555,420,605,370,645,370,705,430,760,510,835,510,1024,40,1024,50,740,170,610,290,560);
            library.directions[i].fistRegion = P(600,500,680,510,720,570,760,610,800,700,860,780,860,1024,500,1024,500,780,590,720,600,650,570,610);
            library.directions[i+1].scytheRegion = P(390,530,500,540,520,620,510,695,540,745,670,790,705,920,705,1024,220,1024,220,700,280,610);
            library.directions[i+2].scytheRegion = P(525,510,645,535,680,655,680,720,705,760,730,790,920,810,960,1024,540,1024,450,850,450,650,490,590);
            library.directions[i+2].fistRegion = P(690,590,825,590,895,680,875,810,705,795,675,710);
            library.directions[i+4].scytheRegion = P(695,510,785,485,950,540,980,900,760,925,740,780,710,740,665,700,670,630,710,585);
            library.directions[i+4].fistRegion = P(265,480,365,510,365,580,340,630,330,710,310,750,300,800,280,860,50,880,50,610,175,530);
        }
        var p1ur = library.directions[3]; p1ur.scytheJoint = V(690,610); p1ur.scytheTip = V(800,720);
        p1ur.scytheRegion = P(650,540,780,530,1024,600,1024,920,680,920,680,720,640,650);
        p1ur.fistRegion = P(420,410,535,440,500,540,400,665,235,675,230,530,320,455);
        var p1ul = library.directions[5]; p1ul.scytheJoint = V(600,480); p1ul.scytheTip = V(675,570);
        p1ul.scytheRegion = P(560,440,700,430,820,510,850,700,630,700,600,625,550,580,540,520);
        library.directions[7].fistRegion = P(485,535,600,565,660,635,690,690,700,745,760,780,790,1024,385,1024,385,825,480,730,530,650);
        library.directions[8].fistJoint = V(625,555);
        library.directions[9].scytheJoint = V(435,595);
        library.directions[10].scytheRegion = P(485,500,600,515,655,620,670,700,680,740,800,790,920,810,960,1024,500,1024,430,820,420,650,460,580);
        library.directions[11].scytheRegion = P(650,515,745,545,800,595,850,650,880,720,910,760,970,970,780,970,750,790,680,700,645,625);
        library.directions[11].fistRegion = P(420,410,535,440,500,540,425,565,370,665,235,675,230,530,320,455);
        library.directions[13].scytheRegion = P(605,465,710,470,835,560,895,775,780,775,750,730,700,660,640,625,625,580,610,545);
        library.directions[14].scytheRegion = P(215,555,300,565,330,620,325,675,365,720,375,875,250,970,20,985,15,640);
        library.directions[14].fistRegion = P(440,615,540,615,635,720,680,1024,340,1024,335,805,390,745,425,705);
        library.directions[15].scytheRegion = P(180,545,275,560,295,645,315,690,335,720,385,750,410,950,20,980,0,650,105,570);
        for (int i = 1; i <= 9; i += 8)
            library.directions[i].scytheRegion = P(390,530,500,540,520,620,510,695,540,745,670,790,705,920,705,1024,250,1024,250,760,270,700,295,620);
        library.directions[6].scytheRegion = P(225,610,315,625,330,670,300,725,265,775,230,810,200,880,40,920,40,630);
        library.directions[14].scytheRegion = P(215,555,300,565,330,620,325,675,354,720,358,855,250,970,20,985,15,640);
        library.directions[14].fistRegion = P(440,615,540,615,635,720,680,1024,350,1024,358,795,390,745,425,705);
        foreach (var rig in library.directions) HiveTyrantSkinAuthoring.Bake(rig);
        library.authoringVersion = 8; EditorUtility.SetDirty(library);
    }
    private static Vector2 V(float x, float y) => new Vector2(x, y);
    private static Vector2[] P(params float[] values)
    { var result = new Vector2[values.Length / 2]; for (int i = 0; i < result.Length; i++) result[i] = V(values[i * 2], values[i * 2 + 1]); return result; }
    private static Vector2[] Shift(Vector2[] source, Vector2 offset)
    { var result = new Vector2[source.Length]; for (int i = 0; i < result.Length; i++) result[i] = source[i] + offset; return result; }
}
