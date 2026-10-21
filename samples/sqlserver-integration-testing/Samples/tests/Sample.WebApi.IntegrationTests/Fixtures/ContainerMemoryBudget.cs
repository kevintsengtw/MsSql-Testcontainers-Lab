using System.Globalization;
using Docker.DotNet.Models;
using Testcontainers.MsSql;

namespace Sample.WebApi.IntegrationTests.Fixtures;

/// <summary>
/// 容器的記憶體預算：容器總量（cgroup）與 SQL Server 行程本身是兩層不同的東西，
/// 本範例明確設定兩層額度，為 tmpfs 與其他行程保留空間。
/// </summary>
public static class ContainerMemoryBudget
{
    /// <summary>
    /// SQL Server 行程自己的記憶體上限（MB）。
    ///
    /// 過去在本機將上限設為 2048 MB 時，<c>CREATE DATABASE</c> 失敗，
    /// 錯誤是 <c>There is insufficient system memory in resource pool 'default' to run this query.</c>
    /// 這是當時環境的觀察，不是所有 SQL Server 版本的最低安全值。
    /// 此上限涵蓋 buffer pool、SQLPAL、query workspace 等元件的總量。
    /// </summary>
    public const int SqlServerLimitMb = 2560;

    // 第一層：容器總量（cgroup），Docker 分配給這個容器的記憶體上限，對應 docker run --memory。
    // 它要裝的不只 SQL Server 的 buffer pool，還有執行緒堆疊、query workspace，
    // 以及 tmpfs 的資料庫檔案內容。tmpfs 的頁面算在容器的 memory cgroup 額度裡，
    // 這點很容易被忽略。
    // SQL Server on Linux 的官方最低需求是 2 GB，這裡取 4 GB 是為了讓上述各項都有餘裕。
    /// <summary>容器總量上限（bytes），對應 docker run --memory。</summary>
    public const long ContainerLimitBytes = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// 單一 tmpfs 掛載點的大小上限（bytes）。不設的話預設是可用記憶體的一半。
    /// /var/opt/mssql/data 最多使用 512 MB，加上 SQL Server 的 2.5 GB，
    /// 仍為容器本身與短暫資源尖峰保留約 1 GB。這個上限只是保險。
    /// </summary>
    public const long TmpfsSizeLimitBytes = 512L * 1024 * 1024;

    /// <summary>
    /// 一次套用兩層記憶體上限與 tmpfs 的大小限制。
    /// 必須在 <c>WithTmpfsMount</c> 之後呼叫，因為 size 選項是就地補進既有掛載點的。
    /// </summary>
    public static MsSqlBuilder WithMemoryBudget(this MsSqlBuilder builder)
    {
        // SQL Server 2022 CU20 起支援 cgroup v2。即使能辨識容器上限，
        // tmpfs 與其他行程仍使用同一個額度，因此明確設定較低的 SQL Server 上限。
        // memorylimitmb 要低於主機記憶體與 cgroup 上限。
        // https://learn.microsoft.com/sql/linux/configure/performance-best-practices-sql-server-memory
        //
        // 預算怎麼算：容器 4 GB ＝ SQL Server 本體上限 2.5 GB
        // ＋ /var/opt/mssql/data 的 tmpfs 上限 512 MB
        // ＋約 1 GB 給其餘行程與短暫資源尖峰。實際需求仍依工作負載而不同。
        return builder
            // MSSQL_MEMORY_LIMIT_MB 注意是三個底線；mssql.conf 裡的同名設定才是無底線的
            // memorylimitmb。支援 cgroup 的版本未設定時，使用主機與 cgroup 較小額度的 80%。
            .WithEnvironment(
                "MSSQL_MEMORY_LIMIT_MB",
                SqlServerLimitMb.ToString(CultureInfo.InvariantCulture))
            .WithCreateParameterModifier(parameters =>
            {
                var hostConfig = parameters.HostConfig ??= new HostConfig();
                hostConfig.Memory = ContainerLimitBytes;

                // WithTmpfsMount 沒有可以指定大小的多載（只有 destination 與 AccessMode），
                // 所以 size 只能從這裡補。
                //
                // 陷阱：Testcontainers 把 tmpfs 掛載放進 HostConfig.Mounts（Type = "tmpfs"），
                // 此建置流程的 HostConfig.Tmpfs 字典為 null；應修改既有 Mount 的 TmpfsOptions，
                // 只檢查 Tmpfs 字典會漏掉 WithTmpfsMount 建立的掛載。
                if (hostConfig.Mounts is null)
                {
                    return;
                }

                foreach (var mount in hostConfig.Mounts)
                {
                    if ("tmpfs".Equals(mount.Type, StringComparison.OrdinalIgnoreCase))
                    {
                        mount.TmpfsOptions = new TmpfsOptions { SizeBytes = TmpfsSizeLimitBytes };
                    }
                }
            });
    }
}
