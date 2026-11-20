using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools
{
    internal static partial class NavalBaseBuilder
    {
        private static void BuildRadarKnoll()
        {
            const int sides = 40;
            var vertices = new List<Vector3> { new(830f, 12f, 20f) };
            var triangles = new List<int>();
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = ring == 0 ? .65f : ring == 1 ? .81f : 1f;
                float height = ring == 0 ? 12f : ring == 1 ? 9f : Ground - .05f;
                for (int i = 0; i < sides; i++)
                {
                    float angle = 2f * Mathf.PI * i / sides;
                    float c = Mathf.Cos(angle);
                    vertices.Add(new Vector3(830f + c * radius * (c < 0 ? 58f : 71f),
                        height, 20f + Mathf.Sin(angle) * radius * 75f));
                    int current = 1 + ring * sides + i;
                    int next = 1 + ring * sides + (i + 1) % sides;
                    if (ring == 0) triangles.AddRange(new[] { 0, next, current });
                    else
                    {
                        triangles.AddRange(new[] { current - sides, next, current });
                        triangles.AddRange(new[] { current - sides, next - sides, next });
                    }
                }
            }
            MeshObject("RadarHill", vertices, triangles, M("grass_dark"));
        }

        private static void TaxiConnector(Transform air, float x)
        {
            int turn = x == -760f ? 1 : x == 660f ? -1 : 0;
            if (turn != 0)
            {
                TaxiElbow(air,x,turn);
                return;
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float[] z = { -311f, -307.5f, -302f, -297f, -289f, -282f, -278f, -274f, -270f, -266f, -262f };
            float[] w = { 18f, 18f, 15.5f, 12.5f, 11.5f, 11.5f, 12.5f, 15.5f, 18f, 20f, 21f };
            for (int i = 0; i < z.Length; i++)
            {
                vertices.Add(new Vector3(x - w[i], Ground + .43f, z[i]));
                vertices.Add(new Vector3(x + w[i], Ground + .43f, z[i]));
                if (i > 0)
                {
                    int j = i * 2;
                    triangles.AddRange(new[] { j-2,j,j-1,j-1,j,j+1 });
                }
            }
            MeshObject($"TaxiConnector_{x:0}",vertices,triangles,M("runway")).transform.SetParent(air,false);

            B(air,new Vector3(x,Ground+.465f,(-307.5f+TaxiZ)*.5f),
                new Vector3(.7f,.035f,TaxiZ+307.5f),"mark_yellow");
        }

        private static void TaxiElbow(Transform air,float x,int turn)
        {
            var path = new List<Vector2> { new(x,-311f),new(x,-307.5f),new(x,-300f),new(x,-276f) };
            for (int i = 1; i <= 12; i++)
            {
                float t = i/12f;
                path.Add(new(x+turn*18f*t*t,TaxiZ-14f*(1f-t)*(1f-t)));
            }
            path.Add(new(x+turn*27f,TaxiZ));
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            for (int i=0;i<path.Count;i++)
            {
                Vector2 direction=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
                Vector2 across=new(direction.y,-direction.x);
                float half=i<2?15f:11.5f;
                foreach (float side in new[]{-1f,1f})
                {
                    Vector2 edge=path[i]+across*(half*side);
                    vertices.Add(new Vector3(edge.x,Ground+.43f,edge.y));
                }
                if (i>0)
                {
                    int j=2*i;
                    triangles.AddRange(new[]{j-2,j,j-1,j-1,j,j+1});
                    if (i==path.Count-1 || i==1) continue;
                    var previous=path[i-1]; var current=path[i];
                    PavementLink(air,new(previous.x,Ground+.465f,previous.y),
                        new(current.x,Ground+.465f,current.y),.72f,.038f,"mark_yellow");
                }
            }
            MeshObject($"TaxiConnector_{x:0}",vertices,triangles,M("runway")).transform.SetParent(air,false);
        }

        private static void BuildDistrictConnections()
        {
            var parking = Group("Parking Access");
            Access(parking,9.5f,new(110,119),new(110,166));
            Access(parking,8.5f,new(270,446),new(270,320),new(213,320));
            Access(parking,8.0f,new(-10,446),new(-10,409));
            Access(parking,9.0f,new(368,119),new(368,142));
            Access(parking,8.0f,new(-850,119),new(-850,146));
            Access(parking,8.0f,new(-380,-57),new(-380,-89));

            var life = Group("Living District");
            Walk(life,new(296,-37),new(296,89),3.4f);
            Walk(life,new(296,86),new(352,86),3.4f);
            Walk(life,new(352,86),new(352,98),3.4f);
            Walk(life,new(352,122),new(352,128),3.4f);
            Walk(life,new(352,128),new(363,128),3.4f);
            Walk(life,new(368,147),new(368,364),3.4f);
            foreach (float z in new[] { 168f,248f,328f })
                Walk(life,new(348,z),new(390,z),3.4f);
            for (int i = -3; i <= 3; i++)
                B(life,new Vector3(352,Ground+.449f,110+i*2.8f),new Vector3(4.7f,.024f,1.25f),"mark_white");
            Marker(life,"LIVING QUARTERS",new Vector3(327,Ground,95),0,"roof_green",21f);

            var air = Group("Aviation District");
            Fence(air,new Vector3(-418,0,-69),new Vector3(-389,0,-69));
            Fence(air,new Vector3(-370,0,-69),new Vector3(-350,0,-69));
            Fence(air,new Vector3(390,0,-229),new Vector3(390,0,-181));
            Marker(air,"NAVAL AIR STATION",new Vector3(-359,Ground,-74),180f,"roof_blue",23f);
            B(air,new Vector3(425,Ground+.16f,-116),new Vector3(8f,.32f,22f),"concrete_light");
            Marker(air,"AIR SERVICE",new Vector3(426,Ground,-115),180f,"roof_blue",17f);

            var navy = Group("Fleet District");
            Access(navy,14f,new(110,447),new(110,392));
            Marker(navy,"FLEET COMMAND",new Vector3(137,Ground,425),0,"roof_slate",24f);
            Walk(navy,new(128,427),new(128,382),4.2f);
            Fence(navy,new Vector3(40,0,423),new Vector3(99,0,423));
            Fence(navy,new Vector3(122,0,423),new Vector3(186,0,423));
            Reserve(37,413,190,430);

            var radar = Group("Radar Approach");
            B(radar,new Vector3(779,Ground+.14f,20),new Vector3(18f,.28f,21f),"concrete_light");
            B(radar,new Vector3(772,Ground+.18f,20),new Vector3(11f,.36f,8.6f),"road");
            Reserve(769,6,798,34);
            Reserve(-418,259,-349,310);

        }

        private static void Access(Transform parent, float width, params Vector2[] points)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 a = points[i-1], b = points[i];
                PavementLink(parent,new(a.x,Ground+.10f,a.y),new(b.x,Ground+.10f,b.y),width+3f,.20f,"sidewalk");
                PavementLink(parent,new(a.x,Ground+.18f,a.y),new(b.x,Ground+.18f,b.y),width,.36f,"road");
                Reserve(Mathf.Min(a.x,b.x)-width,Mathf.Min(a.y,b.y)-width,
                    Mathf.Max(a.x,b.x)+width,Mathf.Max(a.y,b.y)+width);
                if (i < points.Length-1)
                    C(parent,new Vector3(b.x,Ground+.177f,b.y),new Vector3(width,.36f,width),"road");
            }
        }

        private static void Walk(Transform parent, Vector2 a, Vector2 b, float width)
        {
            PavementLink(parent,new(a.x,Ground+.14f,a.y),new(b.x,Ground+.14f,b.y),width,.28f,"sidewalk");
            Reserve(Mathf.Min(a.x,b.x)-width,Mathf.Min(a.y,b.y)-width,
                Mathf.Max(a.x,b.x)+width,Mathf.Max(a.y,b.y)+width);
        }

        private static void PavementLink(Transform parent,Vector3 a,Vector3 b,float width,float depth,string material)
        {
            Vector3 delta = b-a;
            float yaw = Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            B(parent,(a+b)*.5f,new Vector3(width,depth,delta.magnitude+.03f),material,yaw);
        }

        internal static void Marker(Transform parent,string title,Vector3 position,float yaw,string color,float width)
        {
            Transform sign = G(parent,title,position,yaw);
            B(sign,new Vector3(0,.22f,0),new Vector3(width+2f,.44f,3.8f),"concrete");
            foreach (float s in new[] { -1f,1f })
                B(sign,new Vector3(s*(width*.5f-1.15f),2.65f,0),new Vector3(1.15f,5.3f,1.15f),"steel");
            B(sign,new Vector3(0,4.3f,0),new Vector3(width,2.85f,.72f),color);
            B(sign,new Vector3(0,3.05f,.38f),new Vector3(width-.6f,.30f,.08f),"trim_white");
            var label = new GameObject("Sign lettering",typeof(TextMesh));
            label.transform.SetParent(sign,false);
            label.transform.localPosition=new Vector3(0,4.50f,.395f);
            label.transform.localRotation=Quaternion.Euler(0,180f,0);
            var text=label.GetComponent<TextMesh>();
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            text.text=title;
            text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;
            text.fontSize=86;
            text.characterSize=.23f;
            text.color=Color.white;
        }
    }
}
