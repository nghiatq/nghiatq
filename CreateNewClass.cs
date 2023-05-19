using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Dynamic;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AddingPropertiesToClass
{
    public class Program
    {
        static void Main(string[] args)
        {
            // test code
            Test t = new Test() { Name = "nghia", Age = 36, Address = "HN VN", City = "HN", Region = "S" };
            for (int i = 0; i < 3; i++)
            {
                t.lstTest.Add(new InnerClass1 { Name = $"name {i}" });
            }
            string[] props = new string[] { "Name", "Age", "lstTest.Id", "lstTest.Name" };
            var obj = ConvertClass.Convert(t, props);
            //Console.WriteLine(obj.ToString());
            //var srcProps = t.GetType().GetProperties().Where(x => props.Any(i => i == x.Name));
            //DynamicClass obj = new DynamicClass();
            //foreach (var prop in srcProps)
            //{
            //    obj.
            //}
            //Console.WriteLine(null != "");
            //Console.WriteLine(DateUtils.IsDate("13/01/2009"));
            //Console.WriteLine(DateUtils.IsDate("2009/01/13"));
            //Console.WriteLine(DateUtils.IsDate("yyyyyyyy"));
            //Console.WriteLine(DateUtils.IsDate("20090113"));
            //Console.WriteLine(("20090113").IsDate());
            Console.ReadLine();
        }
    }

    public static class DateUtils
    {
        private static string[] formats = {"M/d/yyyy", "d/M/yyyy","MM/dd/yyyy", "dd/MM/yyyy","yyyy/M/d", "yyyy/d/M", "yyyy/dd/MM", "yyyy/MM/dd",
                            "M-d-yyyy", "d-M-yyyy","MM-dd-yyyy", "dd-MM-yyyy","yyyy-M-d", "yyyy-d-M", "yyyy/dd/MM", "yyyy-MM-dd",
                            "Mdyyyy", "dMyyyy","MMddyyyy", "ddMMyyyy","yyyyMd", "yyyydM", "yyyyddMM", "yyyyMMdd"};
        public static bool IsDate(this string date)
        {
            var outDate = DateTime.Now;
            return DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out outDate);
        }
    }

    public class DynamicClass : DynamicObject
    {
        Dictionary<string, object> dictionary
        = new Dictionary<string, object>();

        // This property returns the number of elements
        // in the inner dictionary.
        public int Count
        {
            get
            {
                return dictionary.Count;
            }
        }

        // If you try to get a value of a property
        // not defined in the class, this method is called.
        public override bool TryGetMember(
            GetMemberBinder binder, out object result)
        {
            // Converting the property name to lowercase
            // so that property names become case-insensitive.
            string name = binder.Name.ToLower();

            // If the property name is found in a dictionary,
            // set the result parameter to the property value and return true.
            // Otherwise, return false.
            return dictionary.TryGetValue(name, out result);
        }

        // If you try to set a value of a property that is
        // not defined in the class, this method is called.
        public override bool TrySetMember(
            SetMemberBinder binder, object value)
        {
            // Converting the property name to lowercase
            // so that property names become case-insensitive.
            dictionary[binder.Name.ToLower()] = value;

            // You can always add a value to a dictionary,
            // so this method always returns true.
            return true;
        }
    }

    public static class ConvertClass
    {
        public static object Convert<T>(T t, string[] props, Dictionary<string, List<string>> innerObj = null)
        {
            TypeBuilder tb = GetTypeBuilder();
            ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            List<string> lstProp = new List<string>();
            if (innerObj == null)
            {
                innerObj = new Dictionary<string, List<string>>();
            }
            foreach (var p in props)
            {
                if (p.Contains("."))
                {
                    string frontProp = p.Substring(0, p.IndexOf("."));
                    lstProp.Add(frontProp);
                    List<string> list = new List<string>();
                    if (innerObj.ContainsKey(frontProp))
                    {
                        list.AddRange(innerObj[frontProp]);
                        innerObj.Remove(frontProp);
                    }
                    var backProp = p.Substring(p.IndexOf(".") + 1);
                    list.Add(backProp);
                    innerObj.Add(frontProp, list.Distinct().ToList());
                    continue;
                }
                lstProp.Add(p);
            }
            lstProp = lstProp.Distinct().ToList();
            var srcProps = t.GetType().GetProperties().Where(x => lstProp.Any(i => i == x.Name));
            foreach (var tN in lstProp)
            {
                if (!innerObj.ContainsKey(tN))
                {
                    var p = srcProps.FirstOrDefault(x => x.Name == tN);
                    CreateProperty(tb, p.Name, p.PropertyType);
                }
                else
                {
                    var mem = t.GetType().GetMembers().FirstOrDefault(x => x.Name == tN);
                    Type childType = GetType(mem);
                    CreateProperty(tb, tN, childType);
                    TypeBuilder childTB = GetTypeBuilder("BeaDynamicNestedType");
                    ConstructorBuilder childConstructor = childTB.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                    var lstProps = innerObj[tN];
                    var nestProps = childType.GetProperties().Where(x => lstProps.Any(i => i == x.Name));
                    foreach (var pN in nestProps)
                    {
                        CreateProperty(childTB, pN.Name, pN.PropertyType);
                    }
                }
            }
            Type objectType = tb.CreateType();
            var returnObj = Activator.CreateInstance(objectType);
            foreach (var p in srcProps)
            {
                var desProp = objectType.GetProperty(p.Name);
                desProp.SetValue(returnObj, p.GetValue(t));
            }
            return returnObj;
        }

        private static Type GetType(MemberInfo mem)
        {
            switch (mem.MemberType)
            {
                case MemberTypes.Field:
                    string st = ((FieldInfo)mem).FieldType.FullName;
                    if (st.Contains("System.Collections.Generic"))
                    {
                        return ((FieldInfo)mem).FieldType.GenericTypeArguments.Single();
                    }
                    else
                    {
                        return ((FieldInfo)mem).FieldType;
                    }
                case MemberTypes.Property:
                    return ((PropertyInfo)mem).PropertyType;
                default:
                    return mem.MemberType.GetType();
            }
        }

        private static TypeBuilder GetTypeBuilder(string typeSignature = "BeaDynamicType")
        {
            var an = new AssemblyName(typeSignature);
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("BeaModule");
            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                    null);
            return tb;
        }

        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                tb.DefineMethod("set_" + propertyName,
                  MethodAttributes.Public |
                  MethodAttributes.SpecialName |
                  MethodAttributes.HideBySig,
                  null, new[] { propertyType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }
    }

    public class Test
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Region { get; set; }

        public List<InnerClass1> lstTest = new List<InnerClass1>();
    }

    public class InnerClass1
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public int Age { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Region { get; set; }

    }
}
