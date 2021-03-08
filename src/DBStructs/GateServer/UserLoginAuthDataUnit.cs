using ArmyAnt.DataUnit;

namespace ArmyAnt.GateServer
{
    public class UserLoginAuthDataUnit : IDataUnit
    {
        public IDataUnitResolver<IDataUnit<int>, int> Resolver { get; }

        public int ID { get; set; }
    }
}
