using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Company.MyName;

public partial class Log : Entity<Log>
{
    #region 对象操作
    static Log()
    {
        Meta.Table.DataTable.InsertOnly = true;

        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(LinkID));
        // 按天分表
        //Meta.ShardPolicy = new TimeShardPolicy(nameof(ID), Meta.Factory)
        //{
        //    TablePolicy = "{{0}}_{{1:yyyyMMdd}}",
        //    Step = TimeSpan.FromDays(1),
        //};

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
        Meta.Modules.Add<TraceModule>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        // 在新插入数据或者修改了指定字段时进行修正
        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (isNew && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
        }*/
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化Log[日志]数据……");

    //    var entity = new Log();
    //    entity.ID = 0;
    //    entity.Category = "abc";
    //    entity.Action = "abc";
    //    entity.LinkID = 0;
    //    entity.Success = true;
    //    entity.UserName = "abc";
    //    entity.Ex1 = 0;
    //    entity.Ex2 = 0;
    //    entity.Ex3 = 0.0;
    //    entity.Ex4 = "abc";
    //    entity.Ex5 = "abc";
    //    entity.Ex6 = "abc";
    //    entity.TraceId = "abc";
    //    entity.CreateUser = "abc";
    //    entity.CreateUserID = 0;
    //    entity.CreateIP = "abc";
    //    entity.CreateTime = DateTime.Now;
    //    entity.Remark = "abc";
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化Log[日志]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Log FindByID(Int64 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据创建用户、编号查找</summary>
    /// <param name="createUserId">创建用户</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<Log> FindAllByCreateUserIDAndID(Int32 createUserId, Int64 id)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.CreateUserID == createUserId && e.ID == id);

        return FindAll(_.CreateUserID == createUserId & _.ID == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="category">类别</param>
    /// <param name="action">操作</param>
    /// <param name="linkId">链接</param>
    /// <param name="createUserId">创建用户</param>
    /// <param name="start">时间开始</param>
    /// <param name="end">时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Log> Search(String category, String action, Int32 linkId, Int32 createUserId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!action.IsNullOrEmpty()) exp &= _.Action == action;
        if (linkId >= 0) exp &= _.LinkID == linkId;
        if (createUserId >= 0) exp &= _.CreateUserID == createUserId;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Category.Contains(key) | _.Action.Contains(key) | _.UserName.Contains(key) | _.Ex4.Contains(key) | _.Ex5.Contains(key) | _.Ex6.Contains(key) | _.TraceId.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Action From Log Where CreateTime>'2020-01-24 00:00:00' Group By Action Order By Id Desc limit 20
    static readonly FieldCache<Log> _ActionCache = new FieldCache<Log>(nameof(Action))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取操作列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetActionList() => _ActionCache.FindAllName();

    // Select Count(Id) as Id,Category From Log Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    static readonly FieldCache<Log> _CategoryCache = new FieldCache<Log>(nameof(Category))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    #endregion
}
