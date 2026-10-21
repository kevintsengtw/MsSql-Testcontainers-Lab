namespace Sample.WebApi.IntegrationTests.Fixtures;

/// <summary>
/// 所有整合測試都掛在這個 collection 底下：
/// 1. 共享同一個 SQL Server 容器（Collection Fixture 的生命週期涵蓋整個 collection）
/// 2. collection 內的測試循序執行，避免共用資料庫時的互相干擾
/// 注意：沒有掛上 [Collection] 的測試類別會各自成為「隱含 collection」而平行執行，
/// 新增測試類別時務必記得掛上這個屬性。
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "IntegrationTests";
}
