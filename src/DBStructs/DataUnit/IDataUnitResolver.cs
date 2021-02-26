
namespace ArmyAnt.DataUnit
{
    public interface IDataUnitResolver<T, T_ID> 
        where T:IDataUnit<T_ID>
    {
        string TableName { get; }

        bool InitTable();

        T GetOneData(T_ID id);

        T[] QueryData(T condition);

        int InsertData(params T[] data);

        int UpdateData(params T[] data);

        int InsertOrUpdateData(params T[] data);

        int RemoveData(params T_ID[] id);

        int RemoveData(params T[] condition);
    }

    public interface IDataUnitResolver<T> : IDataUnitResolver<T, int> 
        where T : IDataUnit<int>
    {

    }
}
