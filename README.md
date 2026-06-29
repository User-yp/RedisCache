# RedisCache

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/StackExchange.Redis-2.8.0-aa3b42?logo=redis)](https://www.nuget.org/packages/StackExchange.Redis)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE.txt)

**高性能 Redis 缓存组件** — 通过 TPL Dataflow 缓冲写入 Redis，达到阈值后自动刷入数据库。

你只需要定义实体、注册回调，剩下的：**Dataflow 缓冲 → Redis Hash 写入 → 分布式锁保护 → 批量刷盘**，全部自动完成。

---

## 核心特性

- ⚡ **TPL Dataflow 缓冲** — 高并发写入先入缓冲区，批量写入 Redis，平滑流量峰值
- 🔒 **分布式锁 + Lua 脚本** — 基于 Redis `TIME` 的原子锁，多进程安全（支持锁重试 + 指数退避）
- 📊 **本地计数器** — 避免每次写入查 Redis 长度，达到阈值自动触发刷盘
- 🔄 **定时轮询** — 可选的 `IsPolling` 模式，按间隔自动将 Redis 数据刷入数据库
- 🛡️ **数据不丢失** — "先刷数据库 → 成功才删 Redis"，异常时数据原封不动
- 🔧 **ORM 无关** — 通过回调委托实现刷盘，支持 EF Core / Dapper / ADO.NET / 任何 ORM
- 📦 **最小依赖** — 仅依赖 `StackExchange.Redis` + `Newtonsoft.Json` + `Microsoft.Extensions.*`
- ✅ **完整测试** — 46 个单元测试，覆盖并发、异常、边界值

---

## 安装

```bash
dotnet add package RedisCache
```

或直接引用项目：

```xml
<ProjectReference Include="..\RedisCache\RedisCache.csproj" />
```

---

## 快速开始

### 1. 定义实体

```csharp
using RedisCache.Attributes;

[RedisEntity]               // 标记为缓存实体
public class User
{
    [RedisKey]              // 标记为 Redis Hash 的 field key
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
}
```

### 2. 配置（appsettings.json）

```json
{
  "RedisOption": {
    "ConnectionString": "localhost:6379",
    "DbNumber": 0,
    "Threshold": 3,        // Hash 长度达到 3 触发刷盘
    "IsPolling": false,    // false = 即时模式；true = 定时轮询
    "Interval": 60,        // 轮询间隔（秒）
    "LockKey": "app_lock", // 分布式锁的 Hash 键名
    "Expiry": 30           // 锁过期时间（秒）
  }
}
```

### 3. 注册服务

```csharp
using RedisCache;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 注册 DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 注册 RedisCache（一行回调搞定）
builder.Services.AddRedisCache(builder.Configuration, onFlush: async (sp, key, entities) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.AddRange(entities);          // EF Core 自动识别实体类型
    await db.SaveChangesAsync();
    return true;
});
```

### 4. 写入数据

```csharp
public class UserService
{
    private readonly IRedisCache _cache;

    public UserService(IRedisCache cache) => _cache = cache;

    public async Task CreateUser(User user)
    {
        await _cache.PostRedisAsync(user);   // 入缓冲区 → 自动写 Redis → 达到阈值自动刷 DB
    }

    public async Task BatchCreate(List<User> users)
    {
        await _cache.PostRedisAsync(users.Cast<object>().ToList());
    }
}
```

---

## 架构

```
┌─────────────────────────────────────────────────────────┐
│                    应用层                                 │
│  PostRedisAsync(entity)  ──→  ActionBlock (Dataflow)    │
│                                                          │
│  [RedisEntity] User          回调委托                    │
│  [RedisEntity] Order  ──→  onFlush(sp, key, entities)   │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│                   RedisCache 核心库                       │
│                                                          │
│  写入缓冲层                  数据刷盘层                    │
│  ┌──────────────────┐    ┌──────────────────────────┐   │
│  │ ActionBlock(key)  │    │ WriteDataBaseAsync()      │   │
│  │ BoundedCapacity   │    │ ① 本地计数器 >= 阈值?     │   │
│  │ = 10000           │    │ ② AcquireLock (重试)      │   │
│  │ MaxParallelism    │    │ ③ HSCAN 分页读取 Hash     │   │
│  │ = 20              │    │ ④ onFlush 回调            │   │
│  └──────┬───────────┘    │ ⑤ 成功 → HDEL 删 Redis    │   │
│         │                │    失败 → 数据原封不动      │   │
│         ▼                └──────────────────────────┘   │
│  WriteRedisAsync()                                       │
│  ── HSET 写入 Redis Hash                                 │
│  ── 本地计数器 +1                                         │
│                                                          │
│  RedisService                                             │
│  ┌──────────────────────────────────────────────────┐   │
│  │ • Hash 操作 (HSET/HGET/HDEL/HSCAN)                │   │
│  │ • 分布式锁 (Lua 脚本 + Redis TIME)                 │   │
│  │ • 锁重试 + 指数退避                                │   │
│  │ • 锁续期 (PeriodicTimer)                           │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│                     Redis                                 │
│  HSET User {guid1} "{...json...}"                        │
│  HSET User {guid2} "{...json...}"                        │
│  HSET Order {ord1} "{...json...}"                        │
│  ...                                                      │
└─────────────────────────────────────────────────────────┘
```

---

## 数据流详解

### 即时模式 (`IsPolling: false`)

```
PostRedisAsync(user)
    │
    ▼
ActionBlock.SendAsync(user)          ← 进入 Dataflow 缓冲区
    │
    ▼
WriteRedisAsync(user)                ← ActionBlock 回调
    ├── localCounters["User"] += 1   ← 原子递增本地计数器
    ├── HSET User {guid} "{json}"   ← 写入 Redis Hash
    └── 计数器 >= Threshold(3)?
        ├── 是 → WriteDataBaseAsync()
        │       ├── AcquireLock (重试 3 次, 退避 100/200/400ms)
        │       ├── HSCAN User 分页读取全部 fields
        │       ├── 反序列化 → List<User>
        │       ├── onFlush(sp, "User", entities)  ← 你的回调
        │       ├── 成功 → HDEL 删除 Redis 数据 → 计数器 -= 已删数
        │       └── 失败 → Redis 数据原封不动, 等待下次写入重试
        └── 否 → 继续缓冲
```

### 轮询模式 (`IsPolling: true`)

```
每隔 Interval 秒:
    Parallel.ForEachAsync(["User", "Order", ...])
        ├── GetHashLength("User")     → 同步计数器
        ├── 计数器 >= Threshold?
        │   └── 是 → WriteDataBaseAsync("User")
        └── 同 Key 串行（SemaphoreSlim 保护）
```

---

## 配置选项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `ConnectionString` | `string` | — | Redis 连接字符串（必需） |
| `DbNumber` | `int` | `0` | Redis 数据库编号 |
| `Threshold` | `int` | — | 刷盘阈值（Hash 长度达到此值时触发） |
| `IsPolling` | `bool` | `false` | `false`=每次写入检查阈值 / `true`=定时轮询 |
| `Interval` | `int` | `60` | 轮询间隔（秒，仅 `IsPolling=true` 时生效） |
| `LockKey` | `string` | — | 分布式锁在 Redis 中的 Hash 键名 |
| `Expiry` | `int` | `30` | 锁的过期时间（秒） |

### 模式选择

| 场景 | 推荐模式 | 配置 |
|------|---------|------|
| 实时写入、数据量小 | 即时模式 | `IsPolling: false, Threshold: 3~10` |
| 高吞吐、批量写入 | 即时模式 | `IsPolling: false, Threshold: 50~100` |
| 允许延迟、批量刷盘 | 轮询模式 | `IsPolling: true, Interval: 30~60` |
| 只缓存不刷数据库 | 无回调 | `onFlush: null`（数据仅存 Redis） |

---

## 高级用法

### 自定义 ORM / 多数据库

```csharp
// Dapper 示例
services.AddRedisCache(config, onFlush: async (sp, key, entities) =>
{
    using var scope = sp.CreateScope();
    var conn = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    // 根据 key 分发到不同表
    await conn.ExecuteAsync($"INSERT INTO {key} VALUES (...)", entities);
    return true;
});

// 多数据库分发
services.AddRedisCache(config, onFlush: async (sp, key, entities) =>
{
    using var scope = sp.CreateScope();
    switch (key)
    {
        case nameof(User):
            var userDb = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            userDb.Users.AddRange(entities.Cast<User>());
            await userDb.SaveChangesAsync();
            break;
        case nameof(Order):
            var orderDb = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            orderDb.Orders.AddRange(entities.Cast<Order>());
            await orderDb.SaveChangesAsync();
            break;
    }
    return true;
});
```

### 多进程部署

组件内置分布式锁保证多进程安全：

```
进程 A                              进程 B
  │ WriteDataBaseAsync                 │ WriteDataBaseAsync
  ├── AcquireLock("User") ✅          ├── AcquireLock("User") ⏳
  │   ├── HSCAN → 回调 → 成功         │   重试 1: 100ms 后...
  │   └── ReleaseLock ────┐           │   重试 2: 200ms 后...
  │                       │           │   重试 3: 400ms 后...
  │                       ▼           │
  │                    锁已释放  ──────►  AcquireLock("User") ✅
  │                                    └── HSCAN → 回调 → OK
```

### 数据安全

```
刷盘流程：
  ① HSCAN 读取 Redis Hash
  ② onFlush 回调 → 写入数据库
      ├── ✅ 成功 → ③ HashDeleteFieldsAsync → 计数器 -= N
      └── ❌ 失败 → Redis 数据原封不动 → 下次写入/轮询自动重试

无论进程崩溃、DB 异常、网络断开 —— Redis 中的数据永远不会丢失。
唯一的极端情况：数据库已写入但 Redis 删除前崩溃 → 下次会重复写入。
解决：数据库表上添加唯一约束 + 回调中使用 INSERT IGNORE / ON CONFLICT DO NOTHING。
```

---

## API 参考

### IRedisCache

```csharp
public interface IRedisCache
{
    /// <summary>写入单个实体到 Redis 缓冲区</summary>
    Task<bool> PostRedisAsync(object value);

    /// <summary>批量写入实体到 Redis 缓冲区</summary>
    Task<bool> PostRedisAsync(List<object> values);
}
```

### RedisFlushHandler

```csharp
/// <summary>数据刷盘委托</summary>
/// <param name="serviceProvider">根 IServiceProvider</param>
/// <param name="key">实体类型名</param>
/// <param name="entities">待写入数据库的实体列表</param>
/// <returns>true = 成功（删除 Redis），false = 失败（保留 Redis）</returns>
public delegate Task<bool> RedisFlushHandler(
    IServiceProvider serviceProvider,
    string key,
    List<object> entities
);
```

### Attributes

| Attribute | Target | 说明 |
|-----------|--------|------|
| `[RedisEntity]` | Class | 标记为 Redis 缓存实体 |
| `[RedisKey]` | Property | 标记属性作为 Redis Hash 的 field key |

---

## 运行测试

```bash
cd RedisCache
dotnet test
```

测试覆盖：24 + 22 = **46** 个测试

| 测试类 | 数量 | 覆盖范围 |
|--------|------|---------|
| `RedisKeyExtensionTests` | 10 | Key 生成、实体类型发现、属性扫描 |
| `RedisServiceExtensionTests` | 12 | ToRedisValue/ToHashEntries/ToConcurrentDictionary 转换 |
| `BlockTests` | 8 | ActionBlock、RetryCount 并发原子、DoPollingAsync |
| `RedisCacheTests` | 16 | PostRedisAsync、刷盘回调、分布式锁、异常恢复、计数器一致性 |

---

## 项目结构

```
RedisCache/
├── RedisCache/                          # 核心库
│   ├── RedisCache.cs                    # 主组件（Dataflow 缓冲 + 刷盘）
│   ├── Block.cs                         # ActionBlock 调度器 + RedisFlushHandler 委托
│   ├── IRedisCache.cs                   # 公开接口
│   ├── RedisOption.cs                   # 配置模型
│   ├── RedisExtensions.cs               # DI 注册扩展
│   ├── Attributes/
│   │   ├── RedisEntityAttribute.cs      # [RedisEntity] + [RedisKey] Attribute
│   │   └── RedisKeyExtension.cs         # Key 生成 + 实体类型发现（含反射缓存）
│   └── RedisSever/
│       ├── IRedisService.cs             # Redis 操作接口
│       ├── RedisService.cs              # Redis 操作 + 分布式锁（Lua 脚本）
│       └── RedisServiceExtension.cs     # RedisValue/HashEntry 转换
├── RedisCache.Tests/                    # 单元测试（xUnit + Moq）
│   ├── TestEntities.cs                  # 测试实体
│   ├── RedisKeyExtensionTests.cs
│   ├── RedisServiceExtensionTests.cs
│   ├── BlockTests.cs
│   └── RedisCacheTests.cs
└── RedisCache.WebApi/                   # WebApi 示例
    ├── Program.cs                       # 最佳实践启动代码
    ├── TestEntity.cs                    # 示例实体
    ├── BaseDbContext.cs                 # 示例 DbContext
    └── Controllers/TestController.cs    # 测试接口
```

---

## License

MIT

---

## English

**RedisCache** is a high-performance .NET Redis cache component with built-in TPL Dataflow buffering and automatic database flushing.

### Key Features
- **TPL Dataflow buffering** — absorbs traffic spikes with bounded ActionBlock queues
- **ORM-agnostic** — flush via a callback delegate (EF Core / Dapper / Raw ADO.NET / any ORM)
- **Multi-process safe** — distributed locking via Redis Lua scripts with `TIME`-based timestamps
- **Zero data loss** — flush-first-then-delete: failed DB writes leave Redis data untouched
- **Optimized hot path** — cached reflection, single-field HSET, local counters, HSCAN pagination

### Quick Start

```csharp
// 1. Define entity
[RedisEntity]
public class User
{
    [RedisKey] public Guid Id { get; set; }
    public string Name { get; set; }
}

// 2. Register services
services.AddRedisCache(configuration, onFlush: async (sp, key, entities) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.AddRange(entities);           // EF Core auto-routes by CLR type
    await db.SaveChangesAsync();
    return true;
});

// 3. Write
await cache.PostRedisAsync(user);    // Buffered → Redis → Auto-flush to DB
```

### NuGet Dependencies
- `StackExchange.Redis` ≥ 2.8.0
- `Newtonsoft.Json` ≥ 13.0.3
- `Microsoft.Extensions.*` ≥ 8.0.0
