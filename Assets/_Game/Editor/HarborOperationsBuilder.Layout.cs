using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools
{
    public static partial class HarborOperationsBuilder
    {
        private static Vector2 P(float x,float z) => new(x,z);
        private static Vector3 At(Vector2 point,float y) => new(point.x,y,point.y);

        private static List<Vector2> Rounded(float radius, Vector2[] points)
        {
            var result=new List<Vector2>{ points[0] };
            for(int i=1;i<points.Length-1;i++)
            {
                Vector2 here=points[i];
                float r=Mathf.Min(radius,Vector2.Distance(here,points[i-1])*.38f,Vector2.Distance(here,points[i+1])*.38f);
                Vector2 a=Vector2.MoveTowards(here,points[i-1],r), b=Vector2.MoveTowards(here,points[i+1],r);
                for(int k=0;k<=8;k++)
                {
                    float u=k/8f;
                    result.Add(Vector2.Lerp(Vector2.Lerp(a,here,u),Vector2.Lerp(here,b,u),u));
                }
            }
            result.Add(points[points.Length-1]);
            return result;
        }

        private static void Strip(Transform group,List<Vector2> points,float width,float height,string material,float offset=0f)
        {
            var vertices=new List<Vector3>();
            var tris=new List<int>();
            for(int i=0;i<points.Count;i++)
            {
                Vector2 dir=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;
                Vector2 side=new(dir.y,-dir.x);
                vertices.Add(At(points[i]+side*(offset-width*.5f),height));
                vertices.Add(At(points[i]+side*(offset+width*.5f),height));
                int a=2*i;
                if(i>0) tris.AddRange(new[]{a-2,a,a-1,a-1,a,a+1});
            }
            Part(group,material+" surface",Surface(material,vertices,tris),material,Vector3.zero,Vector3.one,Quaternion.identity);
        }

        private static void LaneDashes(Transform group,List<Vector2> points,float width,float height,float spacing)
        {
            float travelled=0f, next=8f;
            for(int i=1;i<points.Count;i++)
            {
                Vector2 a=points[i-1], delta=points[i]-a;
                float len=delta.magnitude;
                while(next<travelled+len-1f)
                {
                    Vector2 at=a+delta*((next-travelled)/len);
                    float yaw=Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg;
                    Box(group,"Dash",At(at,height),new(width,.025f,5.5f),"mark_white",yaw);
                    next+=spacing;
                }
                travelled+=len;
            }
        }

        private static void Road(Transform group,float width,float turn,params Vector2[] knots)
        {
            var path=Rounded(turn,knots);
            Strip(group,path,width+2.8f,4.47f,"concrete_light");
            Strip(group,path,width,4.55f,"road");
            Strip(group,path,.33f,4.58f,"mark_white",width*.5f-.70f);
            Strip(group,path,.33f,4.58f,"mark_white",-(width*.5f-.70f));
            LaneDashes(group,path,.37f,4.60f,17f);
        }

        private static void Rail(Transform group,Vector2 start,Vector2 end,float foot,string color)
        {
            Vector2 d=end-start;
            int count=Mathf.CeilToInt(d.magnitude/14f);
            for(int i=0;i<=count;i++)
            {
                Vector2 p=Vector2.Lerp(start,end,(float)i/count);
                Box(group,"Rail post",At(p,foot+.77f),new(.34f,1.54f,.34f),color);
            }
            foreach (float h in new[]{.68f,1.44f})
                Beam(group,"Rail",At(start,foot+h),At(end,foot+h),.25f,color);
        }

        private static void StopBar(Transform group,float x,float z,float yaw,float width)
        {
            Box(group,"Hold line",new(x,4.64f,z),new(width,.036f,.74f),"mark_white",yaw);
        }

        private static void Roads(Transform parent)
        {
            var work=Group(parent,"Workshop road joints");
            // Mask the protruding ends of the old square corner markings on the additive apron only.
            Box(work,"First apron joint",new(478,4.46f,500),new(36,.14f,36),"concrete_light");
            Box(work,"Second apron joint",new(684,4.46f,506),new(39,.14f,36),"concrete_light");
            Road(work,17.6f,15f,P(470,457),P(470,505),P(692,505),P(691,697));
            Rail(work,P(491,520),P(570,520),4.25f,"steel");
            Rail(work,P(643,520),P(668,520),4.25f,"steel");
            Rail(work,P(676,699),P(676,726),4.25f,"steel_dark");
            Box(work,"Truck turning bay",new(699,4.26f,705),new(43,.33f,24),"concrete_light");
            foreach(float x in new[]{686f,692f,698f,704f,710f})
                Box(work,"Turning bay stripe",new(x,4.45f,716),new(.50f,.04f,7f),"mark_yellow",-27f);
            for(int i=0;i<6;i++) Box(work,"Wheel stop",new(679+i*7,4.62f,719),new(3.9f,.49f,.8f),"steel_dark");

            var freight=Group(parent,"Freight links and gate");
            Road(freight,18f,15f,P(-850,495),P(-850,455),P(-716,455));
            Road(freight,15.3f,14f,P(-400,316),P(-400,284),P(-353,284));
            StopBar(freight,-410,297,0,7.2f);
            StopBar(freight,-385,284,90,10f);
            Gate(freight,-382,284,true);
            for(int i=0;i<5;i++)
                Box(freight,"Apron lane shoulder",new(-842,4.66f,643+i*9.5f),new(.50f,.05f,6f),"mark_yellow");
            Box(freight,"Trailer bay limit",new(-844,4.66f,687),new(7.4f,.05f,.53f),"mark_yellow");

            var air=Group(parent,"Airside access");
            Road(air,13f,12f,P(405,-46),P(405,-155));
            Box(air,"Service turnout",new(400f,4.32f,-155f),new(22f,.30f,13f),"apron");
            Box(air,"Service stop",new(396f,4.49f,-158.5f),new(7.1f,.035f,.44f),"mark_yellow");
            StopBar(air,405,-64,0,11f);
            StopBar(air,405,-143,0,11f);
            Gate(air,405,-139,false);
        }

        private static void Gate(Transform p,float x,float z,bool rotated)
        {
            var g=Group(p,"Service entrance",new(x,4.49f,z));
            if(rotated) g.localRotation=Quaternion.Euler(0,90f,0);
            Box(g,"Security hut",new(10f,2.22f,0),new(5.9f,4.44f,5.4f),"wall_white");
            Box(g,"Roof edge",new(10f,4.58f,0),new(6.5f,.38f,6.1f),"roof_slate");
            Box(g,"Observation window",new(7.02f,2.94f,-.05f),new(.14f,1.46f,3.5f),"glass_blue");
            Box(g,"Equipment door",new(10.0f,1.54f,2.77f),new(1.73f,3.08f,.14f),"steel_dark");
            Box(g,"Gate post",new(-7.5f,1.29f,0),new(.9f,2.58f,.9f),"steel_dark");
            Beam(g,"Open boom",new(-7.5f,2.45f,0),new(-6.6f,8.2f,0),.37f,"wall_white");
            foreach(float x1 in new[]{-7.3f,7.30f})
            {
                Box(g,"Island",new(x1,.22f,-2f),new(1.34f,.44f,6.8f),"concrete");
                Tube(g,"Bollard",new(x1,.91f,-4.6f),.39f,1.82f,"safety_yellow");
            }
        }

        private static void FacilityDetails(Transform g)
        {
            // Side entrance of the existing workshop, facing the yard across its service lane.
            var shop=Group(g,"Repair shop loading equipment",new(695.8f,4.4f,633f));
            Box(shop,"External door frame",new(0,6.5f,0),new(.58f,13.3f,27.6f),"roof_slate");
            for(int i=0;i<10;i++)
                Box(shop,"Door louver",new(-.37f,2f+i*.80f,0),new(.30f,.40f,23f),"steel");
            Box(shop,"Canopy",new(-2.28f,13.16f,0),new(5.05f,.65f,28.2f),"roof_blue");
            Box(shop,"Canopy fascia",new(-4.7f,13.02f,0),new(.35f,1.1f,28.2f),"steel_dark");
            foreach(float z in new[]{-13.5f,13.5f})
            {
                Box(shop,"Bumper",new(-2.8f,1.02f,z),new(1.7f,2.02f,1.35f),"safety_yellow");
                Box(shop,"Task light",new(-4.1f,12.8f,z*.65f),new(.48f,.26f,1.65f),"wall_white");
            }
            for(int i=0;i<3;i++)
            {
                Vector3 p=new(716f,4.42f,681f+i*5.9f);
                Box(g,"Pallet",p+Vector3.up*.30f,new(7.3f,.58f,3.8f),"roof_metal");
                Box(g,"Dock stores",p+Vector3.up*1.48f,new(6.55f,1.88f,3.25f),i==1?"box_green":"box_blue");
            }

            // Cargo office and a covered dock side pick-up point.
            var cargo=Group(g,"Cargo office loading detail",new(-777f,4.36f,512.8f));
            Box(cargo,"Canopy slab",new(0,4.5f,0),new(23f,.59f,6.6f),"roof_blue");
            foreach(float x in new[]{-10.5f,10.5f})
            {
                Box(cargo,"Canopy post",new(x,2.2f,-2.53f),new(.64f,4.4f,.64f),"steel_dark");
                Box(cargo,"Wheel guard",new(x,.69f,-4.15f),new(1.35f,1.38f,1.35f),"safety_yellow");
            }
            Box(cargo,"Board fascia",new(0,4.8f,-3.30f),new(21f,1.02f,.28f),"roof_slate");
            var supply=Group(g,"Warehouse loading docks",new(-340f,4.18f,374f));
            for(int i=-1;i<=1;i++)
            {
                float x=i*14f;
                Box(supply,"Ramp",new(x,.28f,0),new(10.5f,.56f,15f),"concrete_light");
                Box(supply,"Wheel guide",new(x+4.7f,.56f,-4.5f),new(.43f,.22f,7.7f),"mark_yellow");
            }
        }

        private static void AviationApron(Transform group)
        {
            Vector3 p=new(420f,4.29f,-174f);
            Box(group,"Helicopter apron",p,new(34f,.49f,30f),"apron");
            Box(group,"Pad center",p+Vector3.up*.27f,new(26f,.05f,23f),"runway");
            foreach(float x in new[]{-3.1f,3.1f})
                Box(group,"Pad H",p+new Vector3(x,.31f,0),new(1.1f,.045f,10.1f),"mark_white");
            Box(group,"Pad H",p+Vector3.up*.31f,new(6.3f,.045f,1.1f),"mark_white");
            foreach(float z in new[]{-12f,12f})
                foreach(float x in new[]{-13f,13f})
                {
                    Tube(group,"Pad marker housing",p+new Vector3(x,.63f,z),.43f,.62f,"steel_dark");
                    Tube(group,"Pad marker lens",p+new Vector3(x,.95f,z),.32f,.16f,"mark_yellow");
                }
            var wind=Group(group,"Wind pole",new(443f,4.0f,-158f));
            Tube(wind,"Pole base",new(0,.40f,0),.6f,.8f,"concrete");
            Tube(wind,"Wind pole",new(0,4.7f,0),.19f,8.5f,"steel");
            Beam(wind,"Wind sock",new(0,8.5f,0),new(3.8f,7.1f,.45f),.72f,"box_orange");
            Beam(wind,"Wind sock ring",new(.40f,8.38f,.05f),new(1.3f,8.04f,.15f),.78f,"wall_white");
        }

        private static void Container(Transform root)
        {
            Box(root,"ISO container",Vector3.zero,new(3.25f,2.55f,12.2f),"box_white");
            for(int i=0;i<12;i++)
            {
                float z=-5.62f+i*1.02f;
                foreach(float x in new[]{-1.66f,1.66f})
                    Box(root,"Wall rib",new(x,0,z),new(.085f,2.39f,.15f),"concrete_light");
            }
            foreach(float x in new[]{-.85f,.85f})
                Box(root,"Locking rod",new(x,0,-6.18f),new(.11f,2.03f,.16f),"steel_dark");
            foreach(float x in new[]{-1.49f,1.49f})
                foreach(float z in new[]{-5.95f,5.95f})
                    Box(root,"Corner casting",new(x,1.29f,z),new(.28f,.20f,.36f),"steel_dark");
        }

        private static Transform Truck(Transform parent,bool shortTruck,out Transform wheels)
        {
            var root=Group(parent,shortTruck?"Airfield service tug":"Cargo tractor and trailer");
            Box(root,"Chassis",new(0,.95f,0),new(3.6f,.81f,shortTruck?8.2f:18.8f),"steel_dark");
            float front=shortTruck?2.0f:7.6f;
            Box(root,"Cab",new(0,2.43f,front),new(3.6f,2.6f,3.9f),shortTruck?"safety_yellow":"wall_white");
            Box(root,"Windshield",new(0,3.09f,front+1.98f),new(3.08f,1.13f,.15f),"glass_blue");
            Box(root,"Roof",new(0,3.79f,front),new(3.7f,.18f,3.96f),"roof_white");
            Box(root,"Bumper",new(0,1f,front+2.13f),new(3.8f,.43f,.41f),"steel");
            if(shortTruck)
            {
                Tube(root,"Tank",new(0,2.18f,-1.44f),1.52f,4.65f,"concrete_light",Quaternion.Euler(90,0,0));
                Tube(root,"Roof beacon",new(.59f,3.93f,front),.30f,.28f,"box_orange");
            }
            else
            {
                Box(root,"Trailer bed",new(0,2.58f,0),new(3.48f,.60f,13.1f),"steel_dark");
                Box(root,"Rear bumper",new(0,1.64f,-6.62f),new(3.82f,.43f,.45f),"box_red");
            }
            wheels=Group(root,"Wheel groups");
            foreach(float z in shortTruck?new[]{-2.62f,2.66f}:new[]{-5.48f,-3.96f,4.43f,7.86f})
                foreach(float x in new[]{-1.82f,1.82f})
                {
                    var wheel=Group(wheels,"Wheel axle",new(x,.85f,z));
                    Tube(wheel,"Tyre",Vector3.zero,.85f,.60f,"rubber",Quaternion.Euler(0,0,90));
                    Tube(wheel,"Rim",new(Mathf.Sign(x)*.26f,0,0),.47f,.18f,"steel",Quaternion.Euler(0,0,90));
                }
            return root;
        }

        private static Transform HelicopterModel(Transform parent,out Transform rotor,out Transform tail)
        {
            var root=Group(parent,"Harbor rescue helicopter");
            Part(root,"Lower hull",_sphere,"hull_grey",new(0,1.90f,.47f),new(4.2f,3.03f,10.3f),Quaternion.identity);
            Part(root,"Nose glazing",_sphere,"glass_blue",new(0,2.57f,2.78f),new(3.61f,2.28f,5.89f),Quaternion.Euler(13f,0,0));
            Box(root,"Cabin roof",new(0,3.31f,-.75f),new(3.50f,1.0f,5.51f),"roof_white");
            Beam(root,"Tail boom",new(0,2.78f,-2.63f),new(0,4.17f,-10.82f),1.33f,"hull_grey");
            Box(root,"Tail fin",new(0,4.80f,-10.64f),new(.50f,2.57f,1.37f),"box_orange");
            Box(root,"Tail plane",new(0,3.91f,-9.08f),new(5f,.25f,1.33f),"wall_white");
            foreach(float x in new[]{-1.95f,1.95f})
            {
                Box(root,"Cabin stripe",new(x,1.96f,-.73f),new(.11f,.53f,5.4f),"box_orange");
                Beam(root,"Landing skid",new(x,.12f,-3.01f),new(x,.21f,3.92f),.19f,"steel_dark");
                foreach(float z in new[]{-2.2f,1.93f})
                    Beam(root,"Skid strut",new(x,.18f,z),new(x*.67f,.98f,z),.23f,"steel_dark");
            }
            Tube(root,"Rotor mast",new(0,4.22f,-.35f),.22f,.88f,"steel_dark");
            rotor=Group(root,"Main rotor",new(0,4.68f,-.35f));
            foreach(float yaw in new[]{0f,90f,180f,270f})
            {
                Transform blade=Group(rotor,"Blade");
                blade.localRotation=Quaternion.Euler(0,yaw,0);
                Box(blade,"Rotor blade",new(0,0,4.18f),new(.43f,.09f,8.16f),"steel_dark");
                Box(blade,"Blade tip",new(0,.052f,7.96f),new(.46f,.04f,.55f),"mark_yellow");
            }
            tail=Group(root,"Tail rotor",new(.43f,4.3f,-10.45f));
            Box(tail,"Blade",Vector3.zero,new(.12f,2.69f,.19f),"steel_dark");
            Box(tail,"Blade",Vector3.zero,new(.12f,.19f,2.69f),"steel_dark");
            return root;
        }
    }
}

