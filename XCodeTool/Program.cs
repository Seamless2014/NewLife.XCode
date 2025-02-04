﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.Code;

namespace XCodeTool;

class Program
{
    static void Main(string[] args)
    {
        XTrace.UseConsole();

        //if (args.Length == 0)
        {
            Console.WriteLine("NewLife.XCode v{0}", Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Usage: xcode model.xml");
            Console.WriteLine("Upgrade: https://x.newlifex.com/xcodetool.exe");
            Console.WriteLine();
        }

        // 在当前目录查找模型文件
        var file = "";
        if (args.Length > 0) file = args.LastOrDefault();
        //if (file.IsNullOrEmpty())
        //{
        //    var di = Environment.CurrentDirectory.AsDirectory();
        //    // 选当前目录第一个
        //    file = di.GetFiles("*.xml", SearchOption.TopDirectoryOnly).FirstOrDefault(e => e.Name != "XCode.xml")?.FullName;
        //}
        if (!file.IsNullOrEmpty())
        {
            if (!Path.IsPathRooted(file))
            {
                var file2 = Environment.CurrentDirectory.CombinePath(file);
                if (File.Exists(file2)) file = file2;
            }
            if (!File.Exists(file))
            {
                Console.WriteLine("文件不存在：{0}", file);
                return;
            }

            Build(file);
        }
        else
        {
            // 遍历当前目录及子目录
            var files = new List<String>();
            var di = Environment.CurrentDirectory.AsDirectory();
            //foreach (var fi in di.GetFiles("*.xml", SearchOption.TopDirectoryOnly))
            foreach (var fi in di.GetFiles("*.xml", SearchOption.AllDirectories))
            {
                if (!fi.Name.EqualIgnoreCase("XCode.xml"))
                {
                    var txt = File.ReadAllText(fi.FullName);
                    if (txt.Contains("<Tables") || txt.Contains("<EntityModel")) files.Add(fi.FullName);
                }
            }
            //foreach (var item in di.GetDirectories())
            //{
            //    foreach (var fi in item.GetFiles("*.xml", SearchOption.TopDirectoryOnly))
            //    {
            //        var txt = File.ReadAllText(fi.FullName);
            //        if (txt.Contains("<Tables") || txt.Contains("<EntityModel")) files.Add(fi.FullName);
            //    }
            //}

            if (files.Count > 0)
            {
                // 循环处理
                foreach (var item in files)
                {
                    Build(item);
                }
            }
            else
            {
                // 实在没有，释放一个出来
                var ms = Assembly.GetExecutingAssembly().GetManifestResourceStream("XCode.Model.xml");
                var xml = ms.ToStr();

                file = Environment.CurrentDirectory.CombinePath("Model.xml");
                File.WriteAllText(file, xml, Encoding.UTF8);
            }
        }
    }

    /// <summary>生成实体类。调整该方法可以改变生成实体类代码的逻辑</summary>
    /// <param name="modelFile"></param>
    static void Build(String modelFile)
    {
        XTrace.WriteLine("正在处理：{0}", modelFile);

        EntityBuilder.Debug = true;

        // 设置当前工作目录
        PathHelper.BasePath = Path.GetDirectoryName(modelFile);

        // 设置如何格式化字段名，默认去掉下划线并转驼峰命名
        //ModelResolver.Current = new ModelResolver { TrimUnderline = false, Camel = false };

        // 加载模型文件，得到数据表
        var option = new CubeBuilderOption();
        var tables = ClassBuilder.LoadModels(modelFile, option, out var atts);
        EntityBuilder.FixModelFile(modelFile, option, atts, tables);

        XTrace.WriteLine("共有模型：{0}", tables.Count);

        // 是否把扩展属性生成到数据类
        //option.ExtendOnData = true;

        // 是否使用中文名
        option.ChineseFileName = true;

        // 简易模型类名称，如{name}Model。指定后将生成简易模型类和接口，可用于数据传输
        //var modelClass = atts["ModelClass"];
        //var modelInterface = atts["ModelInterface"];
        var modelClass = option.ModelClass;
        var modelInterface = option.ModelInterface;

        // 生成实体类
        {
            var opt = option.Clone() as EntityBuilderOption;
            //opt.BaseClass = null;
            opt.ClassNameTemplate = null;
            opt.ModelNameForCopy = null;
            opt.ModelNameForToModel = modelClass;
            if (!modelInterface.IsNullOrEmpty())
            {
                opt.BaseClass = modelInterface;
                //opt.ModelNameForCopy = modelInterface;
            }
            //else
            if (!modelClass.IsNullOrEmpty())
            {
                opt.ModelNameForCopy = modelClass;
            }
            EntityBuilder.BuildTables(tables, opt);
        }

        // 生成简易模型类
        {
            var opt = option.Clone() as EntityBuilderOption;
            opt.Items.TryGetValue("ModelsOutput", out var output);
            output ??= @".\Models\";
            opt.Output = opt.Output.CombinePath(output);
            opt.BaseClass = null;
            opt.ClassNameTemplate = modelClass;
            opt.ModelNameForCopy = !modelInterface.IsNullOrEmpty() ? modelInterface : modelClass;
            opt.HasIModel = true;
            if (!modelClass.IsNullOrEmpty())
            {
                ClassBuilder.BuildModels(tables, opt);
            }
            else
            {
                var ts = tables.Where(e => !e.Properties["ModelClass"].IsNullOrEmpty()).ToList();
                if (ts.Count > 0)
                {
                    ClassBuilder.BuildModels(ts, opt);
                }
            }
        }

        // 生成简易接口
        {
            var opt = option.Clone() as EntityBuilderOption;
            opt.Items.TryGetValue("InterfacesOutput", out var output);
            output ??= @".\Interfaces\";
            opt.Output = opt.Output.CombinePath(output);
            opt.BaseClass = null;
            opt.ClassNameTemplate = modelInterface;
            opt.ModelNameForCopy = null;
            opt.HasIModel = false;
            if (!modelInterface.IsNullOrEmpty())
            {
                ClassBuilder.BuildInterfaces(tables, opt);
            }
            else
            {
                var ts = tables.Where(e => !e.Properties["ModelInterface"].IsNullOrEmpty()).ToList();
                if (ts.Count > 0)
                {
                    ClassBuilder.BuildInterfaces(ts, opt);
                }
            }
        }

        // 生成数据字典
        {
            var opt = option.Clone();
            HtmlBuilder.BuildDataDictionary(tables, opt);
        }

        // 生成魔方区域和控制器
        {
            var opt = option.Clone() as CubeBuilderOption;
            if (opt.Items != null && !opt.CubeOutput.IsNullOrEmpty())
            {
                opt.BaseClass = null;
                //opt.Namespace = null;

                opt.Output = opt.CubeOutput;
                CubeBuilder.BuildArea(opt);

                CubeBuilder.BuildControllers(tables, opt);
            }
        }
    }
}