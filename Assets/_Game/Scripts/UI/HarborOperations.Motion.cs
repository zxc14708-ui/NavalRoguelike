using UnityEngine;

namespace Game.UI
{
    public sealed partial class HarborOperations
    {
        private readonly struct Key
        {
            public readonly float Time, Yaw;
            public readonly Vector3 Position;
            public Key(float time, float x, float y, float z, float yaw)
            { Time = time; Position = new Vector3(x,y,z); Yaw = yaw; }
        }

        private static readonly Key[] Cargo =
        {
            new(0, 35,-3,85, 0), new(26,35,-3,85,0), new(35,55,-3,101,0),
            new(47,64,-3,230,-7), new(60,4,-3,316,-77), new(73,-157,-3,344,-96),
            new(88,-303,-3,299,-151), new(102,-210,-3,261,91),
            new(115,44,-3,285,0), new(125,52,-3,192,0), new(136,35,-3,85,0),
        };
        private static readonly Key[] Truck =
        {
            new(0,4,4.63f,124,180),new(15,4,4.63f,124,180),new(28,4,4.47f,-68,180),
            new(31,28,4.48f,-95,90),new(50,416,4.49f,-95,90),
            new(54,447,4.49f,-128,180),new(62,447,4.49f,-245,180),
            new(65,470,4.52f,-266,90),new(69,496,4.52f,-266,90),
            new(78,496,4.52f,-266,90),new(81,484,4.52f,-266,90),
            new(85,465,4.51f,-253,-30),new(89,453,4.48f,-232,0),
            new(98,453,4.48f,-124,0),new(101,420,4.49f,-90,270),
            new(122,34,4.50f,-90,270),new(126,4,4.48f,-57,180),
            new(137,4,4.63f,124,180),
        };
        private static readonly Key[] Helicopter =
        {
            new(0,420,4.56f,-174,45),new(9,420,4.56f,-174,45),
            new(15,420,26,-174,70),new(25,605,49,-170,72),
            new(38,935,78,-102,85),new(51,1130,64,23,-15),
            new(64,1050,58,200,-92),new(78,683,57,177,-127),
            new(87,548,38,-82,-138),new(95,420,26,-174,45),new(103,420,4.56f,-174,45),
        };
        private static readonly Key[] Service =
        {
            new(0,405,4.48f,-49,180),new(18,405,4.48f,-49,180),new(25,405,4.48f,-76,180),
            new(36,405,4.48f,-146,180),new(40,398,4.48f,-155,-145),
            new(51,398,4.48f,-155,-145),new(55,405,4.48f,-142,180),
            new(61,405,4.48f,-69,180),new(66,405,4.48f,-49,180),
        };

        private static float Ease(float t, float a, float b) =>
            Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(a,b,t));

        private static Pose On(Key[] keys, float time)
        {
            for (int i=1; i<keys.Length; i++)
            {
                if (time > keys[i].Time) continue;
                Key a=keys[i-1], b=keys[i];
                float u=Ease(time,a.Time,b.Time);
                return new Pose(Vector3.Lerp(a.Position,b.Position,u),
                    Quaternion.Euler(0f,Mathf.LerpAngle(a.Yaw,b.Yaw,u),0f));
            }
            Key end=keys[keys.Length-1];
            return new Pose(end.Position,Quaternion.Euler(0f,end.Yaw,0f));
        }

        private void Sample(float t)
        {
            Pose ship=On(Cargo,t);
            _ship.localPosition=ship.position;
            _ship.localRotation=ship.rotation;
            SampleTugs(t);
            UpdateWake(shipWake,ship,Speed(Cargo,t),72f,14f);

            Pose trailer=On(Truck,t);
            truck.localPosition=_port.localPosition+trailer.position;
            truck.localRotation=trailer.rotation;
            if (truckWheels != null)
            {
                float spin=WheelRotation(t);
                foreach (Transform axle in truckWheels) axle.localRotation=Quaternion.Euler(spin,0f,0f);
            }
            SampleCrane(t);
            Pose service=On(Service,t);
            serviceTruck.localPosition=service.position;
            serviceTruck.localRotation=service.rotation;
            Pose helo=On(Helicopter,t);
            helicopter.localPosition=helo.position;
            helicopter.localRotation=helo.rotation;
            float running=Ease(t,1f,8f)*(1f-Ease(t,103f,112f));
            rotor.localRotation=Quaternion.Euler(0f,t*1220f*running,0f);
            tailRotor.localRotation=Quaternion.Euler(t*1750f*running,0f,0f);
        }

        private static float Speed(Key[] keys, float t) =>
            Vector3.Distance(On(keys,t+.15f).position,On(keys,t-.15f).position)/.3f;

        private static float WheelRotation(float t)
        {
            float travel=0f;
            for (int i=1; i<Truck.Length; i++)
            {
                var a=Truck[i-1]; var b=Truck[i];
                if (t <= a.Time) break;
                travel += Vector3.Distance(a.Position,b.Position)*Ease(t,a.Time,b.Time);
            }
            return travel*Mathf.Rad2Deg/.85f;
        }

        private void UpdateCargoMooring(float t)
        {
            bool berthed=t<26f || t>=136f;
            dockTugLines.GetChild(0).gameObject.SetActive(berthed);
            if (berthed)
            {
                float[] z={20f,50f,130f,160f};
                Transform cargoLines=dockTugLines.GetChild(0);
                for (int i=0;i<z.Length;i++)
                    Stretch(cargoLines.GetChild(i),AtPort(12f,4.9f,z[i]),AtPort(21f,8.2f,z[i]+(i<2?5f:-5f)),.26f);
            }
        }

        private void UpdateWake(Transform wake, Pose vessel, float speed, float stern, float width)
        {
            bool moving=speed>.42f;
            wake.gameObject.SetActive(moving);
            if (!moving) return;
            Vector3 at=vessel.position+vessel.rotation*new Vector3(0,0,-stern);
            at.y=.45f;
            wake.localPosition=_port.localPosition+at;
            wake.localRotation=vessel.rotation;
            wake.localScale=new Vector3(width,1f,Mathf.Clamp(speed*2.1f,3f,28f));
        }

        private void SampleCrane(float t)
        {
            const float pickX=36.7f, padX=4f, rowZ=124f;
            const float shipTop=17.25f, truckTop=10.24f;
            float x,y;
            if (t<3f) { x=pickX; y=Mathf.Lerp(33f,shipTop,Ease(t,0,3)); }
            else if (t<6f) { x=pickX; y=Mathf.Lerp(shipTop,28f,Ease(t,3,6)); }
            else if (t<10f) { x=Mathf.Lerp(pickX,padX,Ease(t,6,10)); y=28f; }
            else if (t<13f) { x=padX; y=Mathf.Lerp(28f,truckTop,Ease(t,10,13)); }
            else if (t<16f) { x=padX; y=Mathf.Lerp(truckTop,27f,Ease(t,13,16)); }
            else if (t<137f) { x=padX; y=27f; }
            else if (t<139f) { x=padX; y=Mathf.Lerp(27f,truckTop,Ease(t,137,139)); }
            else if (t<142f) { x=padX; y=Mathf.Lerp(truckTop,28f,Ease(t,139,142)); }
            else if (t<146f) { x=Mathf.Lerp(padX,pickX,Ease(t,142,146)); y=28f; }
            else if (t<149f) { x=pickX; y=Mathf.Lerp(28f,shipTop,Ease(t,146,149)); }
            else { x=pickX; y=Mathf.Lerp(shipTop,33f,Ease(t,149,153)); }
            Put(trolley,AtPort(x,39.45f,rowZ),0);
            Put(spreader,AtPort(x,y,rowZ),0);
            Stretch(craneCableA,AtPort(x,39.3f,rowZ-2.7f),AtPort(x,y+.25f,rowZ-2.7f),.22f);
            Stretch(craneCableB,AtPort(x,39.3f,rowZ+2.7f),AtPort(x,y+.25f,rowZ+2.7f),.22f);

            if (t<3f || t>=149f)
            {
                transferBox.localPosition=Local(_ship.TransformPoint(new Vector3(1.7f,18.9f,39f)));
                transferBox.localRotation=_ship.localRotation;
            }
            else if (t>=13f && t<139f)
            {
                transferBox.localPosition=Local(truckSocket.position);
                transferBox.localRotation=truck.localRotation;
            }
            else Put(transferBox,AtPort(x,y-1.35f,rowZ),0f);
        }
    }
}

