
namespace ArmyAnt.DataUnit
{
    public interface IDataUnit<T_ID>
    {
        IDataUnitResolver<IDataUnit<T_ID>, T_ID> Resolver { get; }
        T_ID ID { get; }
    }

    public interface IDataUnit : IDataUnit<int>
    {

    }
}
