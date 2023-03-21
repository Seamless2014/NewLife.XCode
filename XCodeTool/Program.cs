﻿using System;
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
            Console.WriteLine();
            //Console.WriteLine("commands:");
            //Console.WriteLine("\tentity\t\tGenerate entity class");
            //Console.WriteLine("\tmodel\t\tGenerate model class");
            //Console.WriteLine("\tinterface\tGenerate interface");
            //Console.WriteLine();
            //Console.WriteLine("options:");
            //Console.WriteLine("\t-output <PATH>\t\t输出目录");
            //Console.WriteLine("\t-baseClass <NAME>\t\t基类。可能包含基类和接口，其中{name}替换为Table.Name");
            //Console.WriteLine("\t-classNameTemplate <NAME>\t类名模板。其中{name}替换为Table.Name，如{name}Model/I{name}Dto等");
            //Console.WriteLine("\t-modelNameForCopy <NAME>\t用于生成拷贝函数的模型类。例如{name}或I{name}");
        }

        var file = "";
        if (args.Length > 0) file = args.LastOrDefault();
        if (file.IsNullOrEmpty())
        {
            var di = Environment.CurrentDirectory.AsDirectory();
            // 选当前目录第一个
            file = di.GetFiles("*.xml", SearchOption.TopDirectoryOnly).FirstOrDefault()?.FullName;
        }
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
            // 实在没有，释放一个出来
            var ms = Assembly.GetExecutingAssembly().GetManifestResourceStream("XCode.Model.xml");
            var xml = ms.ToStr();

            file = Environment.CurrentDirectory.CombinePath("Model.xml");
            File.WriteAllText(file, xml, Encoding.UTF8);
        }
    }

    static void Build(String modelFile)
    {
        Console.WriteLine("正在处理：{0}", modelFile);

        EntityBuilder.Debug = true;

        // 设置当前工作目录
        PathHelper.BasePath = Path.GetDirectoryName(modelFile);

        // 设置如何格式化字段名，默认去掉下划线并转驼峰命名
        //ModelResolver.Current = new ModelResolver { TrimUnderline = false, Camel = false };

        // 加载模型文件，得到数据表
        var option = new BuilderOption();
        var tables = ClassBuilder.LoadModels(modelFile, option, out var atts);
        EntityBuilder.FixModelFile(modelFile, option, atts, tables);

        // 简易模型类名称，如{name}Model。指定后将生成简易模型类和接口，可用于数据传输
        var modelClass = atts["ModelClass"];
        var modelInterface = atts["ModelInterface"];

        // 生成实体类
        {
            var opt = option.Clone();
            //opt.BaseClass = null;
            opt.ClassNameTemplate = null;
            opt.ModelNameForCopy = null;
            opt.ModelNameForToModel = modelClass;
            if (!modelInterface.IsNullOrEmpty())
            {
                //opt.BaseClass = modelInterface;
                opt.ModelNameForCopy = modelInterface;
            }
            else if (!modelClass.IsNullOrEmpty())
            {
                opt.ModelNameForCopy = modelClass;
            }
            EntityBuilder.BuildTables(tables, opt, chineseFileName: true);
        }

        // 生成简易模型类
        {
            var opt = option.Clone();
            opt.Items.TryGetValue("ModelsOutput", out var output);
            output ??= @"..\Models\";
            opt.Output = opt.Output.CombinePath(output);
            opt.BaseClass = modelInterface;
            opt.ClassNameTemplate = modelClass;
            opt.ModelNameForCopy = !modelInterface.IsNullOrEmpty() ? modelInterface : modelClass;
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
            var opt = option.Clone();
            opt.Items.TryGetValue("InterfacesOutput", out var output);
            output ??= @"..\Interfaces\";
            opt.Output = opt.Output.CombinePath(output);
            opt.BaseClass = null;
            opt.ClassNameTemplate = modelInterface;
            opt.ModelNameForCopy = null;
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
            var opt = option.Clone();
            if (opt.Items != null && opt.Items.TryGetValue("CubeOutput", out var output) && !output.IsNullOrEmpty())
            {
                opt.BaseClass = null;
                //opt.Namespace = null;

                opt.Output = output;
                CubeBuilder.BuildArea(opt);

                CubeBuilder.BuildControllers(tables, opt);
            }
        }
    }
}