using UnityEngine;

namespace Game.UI
{
    public sealed partial class HarborOperations
    {
        private const float Depart = 26f;
        private const float Dock = 136f;
        private const float SideOffset = 27f;
        private static readonly Key BowBerth = new(0,-42,-1,53,0);
        private static readonly Key SternBerth = new(0,-42,-1,111,0);

        private sealed class TugRoute
        {
            public readonly float Station, Release, Recall, Connect;
            public readonly Key[] Approach, PeelOff, ReturnToShip, Finish;

            public TugRoute(float station, float release, float recall, float connect,
                Key[] approach, Key[] peelOff, Key[] returnToShip, Key[] finish)
            {
                Station=station; Release=release; Recall=recall; Connect=connect;
                Approach=approach; PeelOff=peelOff; ReturnToShip=returnToShip; Finish=finish;
            }
        }

        private static class ServiceRoutes
        {
            private const float BowStation = 54f, SternStation = -47f;
            private static readonly float SternRelease = FindRelease();
            private static readonly float BowRelease = SternRelease + 3.5f;

            public static readonly TugRoute Stern = new(SternStation,SternRelease,92f,117f,
                new[] { BerthAt(SternBerth,0),BerthAt(SternBerth,2),At(7,-40,201,3),At(10,1,221,86),
                    At(13,65,214,145),At(18,73,150,180),At(22,70,45,180),At(25,65,38,0),Alongside(Depart,SternStation) },
                new[] { Alongside(SternRelease,SternStation),At(SternRelease+1.35f,116,240,-39),
                    At(SternRelease+4,110,219,172),At(SternRelease+8,57,208,-93),At(SternRelease+12,-27,209,-91),
                    At(SternRelease+16,-42,183,180),At(SternRelease+21,-42,112,180),BerthAt(SternBerth,SternRelease+24) },
                new[] { BerthAt(SternBerth,92),At(98,-42,207,0),At(102,13,222,85),At(107,101,214,90),
                    At(112,111,218,0),Alongside(117,SternStation) },
                new[] { Alongside(Dock,SternStation),At(139,91,56,15),At(147,105,197,0),At(151,39,231,-84),
                    At(155,-39,221,-100),At(160,-42,158,180),At(165,-42,112,180),BerthAt(SternBerth,169) });

            public static readonly TugRoute Bow = new(BowStation,BowRelease,95f,120f,
                new[] { BerthAt(BowBerth,0),BerthAt(BowBerth,5),At(12,-43,191,0),At(16,-9,229,79),
                    At(19,49,231,101),At(23,75,186,171),At(25,67,144,3),Alongside(Depart,BowStation) },
                new[] { Alongside(BowRelease,BowStation),At(61,3,383,60),At(66,119,365,120),At(71,128,258,180),
                    At(76,78,218,-100),At(78,-26,215,-94),At(82,-26,147,180),At(86,-26,68,180),BerthAt(BowBerth,90) },
                new[] { BerthAt(BowBerth,95),At(102,-26,153,0),At(108,-26,207,0),At(112,73,218,90),
                    At(116,105,276,0),Alongside(120,BowStation) },
                new[] { Alongside(Dock,BowStation),At(140,67,190,0),At(145,25,218,-85),At(150,-26,204,-100),
                    At(156,-25,151,180),At(161,-25,68,180),BerthAt(BowBerth,165) });

            private static Key At(float time,float x,float z,float yaw) => new(time,x,-1f,z,yaw);
            private static Key BerthAt(Key berth,float time) => At(time,berth.Position.x,berth.Position.z,berth.Yaw);
            private static Key Alongside(float time,float station)
            {
                var pose = AlongsideShip(time,station);
                return At(time,pose.position.x,pose.position.z,pose.rotation.eulerAngles.y);
            }

            private static float FindRelease()
            {
                const float clearanceZ = 180f + 15f;
                for (int step=0; step<=320; step++)
                {
                    float t=47f+step*.05f;
                    Pose ship=On(Cargo,t);
                    Vector3 forward=ship.rotation*Vector3.forward, right=ship.rotation*Vector3.right;
                    float sternZ=ship.position.z-74f*Mathf.Abs(forward.z)-14.8f*Mathf.Abs(right.z);
                    if (sternZ >= clearanceZ) return t;
                }
                return 63f;
            }
        }

        private static Pose AlongsideShip(float t,float station)
        {
            Pose ship=On(Cargo,t);
            Vector3 at=ship.position+ship.rotation*new Vector3(SideOffset,0f,station);
            at.y=-1f;
            return new Pose(at,ship.rotation);
        }

        private static Pose TugPose(TugRoute route,float t)
        {
            if (t<Depart) return On(route.Approach,t);
            if (t<route.Release) return AlongsideShip(t,route.Station);
            if (t<route.Recall) return On(route.PeelOff,t);
            if (t<route.Connect) return On(route.ReturnToShip,t);
            if (t<Dock) return AlongsideShip(t,route.Station);
            return On(route.Finish,t);
        }

        private void SampleTugs(float t)
        {
            SampleTug(ServiceRoutes.Bow,_tug,towLines,tugWake,dockTugLines.GetChild(1),BowBerth,t);
            SampleTug(ServiceRoutes.Stern,_sternTug,sternTowLines,sternTugWake,dockTugLines.GetChild(2),SternBerth,t);
            UpdateCargoMooring(t);
        }

        private void SampleTug(TugRoute route,Transform tug,Transform cable,Transform wake,Transform moor,Key berth,float t)
        {
            Pose pose=TugPose(route,t);
            tug.localPosition=pose.position;
            tug.localRotation=pose.rotation;
            Vector3 motion=TugPose(route,t+.15f).position-TugPose(route,t-.15f).position;
            // Foam follows the direction of travel when a tug backs away from the dock.
            if (motion.sqrMagnitude>.015f)
                UpdateWake(wake,new Pose(pose.position,Quaternion.LookRotation(motion)),motion.magnitude/.3f,9f,4.5f);
            else
                wake.gameObject.SetActive(false);

            bool berthed=Vector3.SqrMagnitude(pose.position-berth.Position)<.025f &&
                         Quaternion.Angle(pose.rotation,Quaternion.identity)<4f;
            moor.gameObject.SetActive(berthed);
            if (berthed)
            {
                float z=berth.Position.z;
                Stretch(moor.GetChild(0),AtPort(-54.3f,4.8f,z+12f),Local(tug.TransformPoint(new(-3.1f,5.3f,6.6f))),.23f);
                Stretch(moor.GetChild(1),AtPort(-54.3f,4.8f,z-12f),Local(tug.TransformPoint(new(-3.1f,5.3f,-6.6f))),.23f);
            }

            bool towing=(t>=Depart && t<route.Release) || (t>=route.Connect && t<Dock);
            cable.gameObject.SetActive(towing);
            if (!towing) return;
            Vector3 a=Local(_ship.TransformPoint(new Vector3(14.25f,10.2f,route.Station)));
            Vector3 b=Local(tug.TransformPoint(new Vector3(-3.2f,5.3f,route.Station>0f?-4f:4f)));
            float age=t<route.Release ? t-Depart : t-route.Connect;
            Vector3 mid=Vector3.Lerp(a,b,.5f)-Vector3.up*(.14f+.55f*(1f-Ease(age,0f,3f)));
            Stretch(cable.GetChild(0),a,mid,.30f);
            Stretch(cable.GetChild(1),mid,b,.30f);
        }
    }
}
