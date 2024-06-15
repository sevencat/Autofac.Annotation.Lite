using System;

namespace Autofac.Annotation.Util
{
	public class ProxyUtil
	{
		public static bool IsAccessible(Type target)
		{
			if ((target.IsPublic ? 1 : (target.IsNestedPublic ? 1 : 0)) != 0)
				return true;
			bool isNested = target.IsNested;
			bool flag = isNested && (target.IsNestedAssembly || target.IsNestedFamORAssem);
			return ((target.IsVisible ? 0 : (!isNested ? 1 : 0)) | (flag ? 1 : 0)) != 0;
		}
	}
}