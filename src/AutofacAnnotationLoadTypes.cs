using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac.Annotation.Condition;
using Autofac.Annotation.Config;
using Autofac.Annotation.Util;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;

namespace Autofac.Annotation
{
    /// <summary>
    ///     Condition 打在类上
    ///     CoditionOnMissingClass ConditionOnClass 打在类和方法
    ///     ConditionOnMissingBean ConditionOnBean 只打在方法
    ///     针对 Compoment Bean AutoConfiguration Import PointCut
    ///     ConditionOnMissingBean ConditionOnBean 只设计在AutoConfiguration下才有效
    ///     由于默认的加载顺序问题 https://zenn.dev/kawakawaryuryu/articles/d97361bcde98ed
    /// </summary>
    public partial class AutofacAnnotationModule
    {
        /// <summary>
        ///     註冊BeanPostProcessor處理器
        /// </summary>
        private void RegisterBeforeBeanPostProcessor<TReflectionActivatorData>(ComponentModel component,
            IRegistrationBuilder<object, TReflectionActivatorData, object> registrar)
            where TReflectionActivatorData : ReflectionActivatorData
        {
            //过滤掉框架类
            if (component.CurrentType.Assembly == GetType().Assembly ||
                component.CurrentType.Assembly == typeof(LifetimeScope).Assembly)
                return;

            if (component.IsBenPostProcessor) return;

            registrar.ConfigurePipeline(p =>
                p.Use(PipelinePhase.Activation, MiddlewareInsertionMode.StartOfPhase, (ctxt, next) =>
                {
                    next(ctxt);
                    DoBeforeBeanPostProcessor(ctxt);
                }));
        }

        private void RegisterAfterBeanPostProcessor<TReflectionActivatorData>(ComponentModel component,
            IRegistrationBuilder<object, TReflectionActivatorData, object> registrar)
            where TReflectionActivatorData : ReflectionActivatorData
        {
            //过滤掉框架类
            if (component.CurrentType.Assembly == GetType().Assembly ||
                component.CurrentType.Assembly == typeof(LifetimeScope).Assembly)
                return;

            if (component.IsBenPostProcessor) return;

            registrar.ConfigurePipeline(p =>
                p.Use(PipelinePhase.Activation, MiddlewareInsertionMode.StartOfPhase, (ctxt, next) =>
                {
                    next(ctxt);
                    DoAfterBeanPostProcessor(ctxt);
                }));
        }

        /// <summary>
        ///     註冊BeanPostProcessor處理器
        /// </summary>
        private void RegisterBeforeBeanPostProcessor(ComponentModel component,
            IRegistrationBuilder<object, ConcreteReflectionActivatorData, SingleRegistrationStyle> registrar)
        {
            if (component.IsBenPostProcessor) return;

            registrar.ConfigurePipeline(p =>
                p.Use(PipelinePhase.Activation, MiddlewareInsertionMode.StartOfPhase, (ctxt, next) =>
                {
                    next(ctxt);
                    DoBeforeBeanPostProcessor(ctxt);
                }));
        }

        private void RegisterBeforeBeanPostProcessor(ComponentModel component, IComponentRegistration registrar)
        {
            if (component.IsBenPostProcessor) return;

            registrar.ConfigurePipeline(p =>
                p.Use(PipelinePhase.Activation, MiddlewareInsertionMode.StartOfPhase, (ctxt, next) =>
                {
                    next(ctxt);
                    DoBeforeBeanPostProcessor(ctxt);
                }));
        }

        private void RegisterAfterBeanPostProcessor(ComponentModel component,
            IRegistrationBuilder<object, ConcreteReflectionActivatorData, SingleRegistrationStyle> registrar)
        {
            if (component.IsBenPostProcessor) return;

            registrar.ConfigurePipeline(p =>
                p.Use(PipelinePhase.Activation, MiddlewareInsertionMode.EndOfPhase, (ctxt, next) =>
                {
                    next(ctxt);
                    DoAfterBeanPostProcessor(ctxt);
                }));
        }

        private void RegisterAfterBeanPostProcessor(ComponentModel component, IComponentRegistration registrar)
        {
            if (component.IsBenPostProcessor) return;

            registrar.ConfigurePipeline(p =>
                p.Use(PipelinePhase.Activation, MiddlewareInsertionMode.EndOfPhase, (ctxt, next) =>
                {
                    next(ctxt);
                    DoAfterBeanPostProcessor(ctxt);
                }));
        }

        /// <summary>
        ///     BeanPostProcessor處理器
        ///     该方法在bean实例化完毕（且已经注入完毕），在afterPropertiesSet或自定义init方法执行之前
        /// </summary>
        /// <param name="context"></param>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private void DoBeforeBeanPostProcessor(ResolveRequestContext context)
        {
            if (!context.ComponentRegistry.Properties.TryGetValue(nameof(List<BeanPostProcessor>), out var temp))
                return;

            if (!(temp is List<BeanPostProcessor> beanPostProcessors) || !beanPostProcessors.Any()) return;

            foreach (var beanPostProcessor in beanPostProcessors)
                context.Instance = beanPostProcessor.PostProcessBeforeInitialization(context.Instance);
        }

        /// <summary>
        ///     BeanPostProcessor處理器
        ///     在afterPropertiesSet或自定义init方法执行之后
        /// </summary>
        /// <param name="context"></param>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private void DoAfterBeanPostProcessor(ResolveRequestContext context)
        {
            if (!context.ComponentRegistry.Properties.TryGetValue(nameof(List<BeanPostProcessor>), out var temp))
                return;

            if (!(temp is List<BeanPostProcessor> beanPostProcessors) || !beanPostProcessors.Any()) return;

            foreach (var beanPostProcessor in beanPostProcessors)
                context.Instance = beanPostProcessor.PostProcessAfterInitialization(context.Instance);
        }

        /// <summary>
        ///     针对Compoment注解
        /// </summary>
        /// <returns></returns>
        internal static bool shouldSkip(IComponentRegistryBuilder context, Type currentType)
        {
            //拿到打在method上的Conditianl标签
            var conditionList = currentType.GetCustomAttributes<Conditional>().ToList();
            if (!conditionList.Any()) return false;

            var cache = new Dictionary<Type, ICondition>();

            foreach (var conditional in conditionList)
            {
                if (conditional.Type == null || typeof(Conditional).IsAssignableFrom(conditional.Type))
                    throw new InvalidCastException(
                        $"`{currentType.Namespace}.{currentType.Name}.` [conditional] load fail,`{conditional.Type?.FullName}` must be implements of `Condition`");

                if (!cache.TryGetValue(conditional.Type, out var condition))
                {
                    condition = Activator.CreateInstance(conditional.Type) as ICondition;
                    if (condition == null) continue;

                    cache.Add(conditional.Type, condition);
                }

                if (condition.ShouldSkip(context, conditional)) return true;
            }

            return false;
        }

        /// <summary>
        ///     注册AutoConfiguration注解标识的类里面的Bean时候的过滤逻辑
        /// </summary>
        /// <returns></returns>
        internal static bool shouldSkip(IComponentRegistryBuilder context, Type currentType, MethodInfo beanMethod)
        {
            //拿到打在method上的Conditianl标签
            var conditionList = beanMethod.GetCustomAttributes<Conditional>().ToList();
            if (!conditionList.Any()) return false;

            var cache = new Dictionary<Type, ICondition>();

            foreach (var conditional in conditionList)
            {
                if (conditional.Type == null || typeof(Conditional).IsAssignableFrom(conditional.Type))
                    throw new InvalidCastException(
                        $"`{currentType.Namespace}.{currentType.Name}.{beanMethod.Name}` [conditional] load fail,`{conditional.Type?.FullName}` must be implements of `Condition`");

                if (!cache.TryGetValue(conditional.Type, out var condition))
                {
                    condition = Activator.CreateInstance(conditional.Type) as ICondition;
                    if (condition == null) continue;

                    cache.Add(conditional.Type, condition);
                }

                if (condition.ShouldSkip(context, conditional)) return true;
            }

            return false;
        }

        /// <summary>
        ///     注册DependsOn
        /// </summary>
        /// <param name="compoment"></param>
        /// <param name="registrar"></param>
        /// <typeparam name="TReflectionActivatorData"></typeparam>
        private void RegisterDependsOn<TReflectionActivatorData>(ComponentModel compoment,
            IRegistrationBuilder<object, TReflectionActivatorData, object> registrar)
            where TReflectionActivatorData : ReflectionActivatorData
        {
            if (compoment.DependsOn == null) return;

            registrar.ConfigurePipeline(p =>
            {
                //DepondsOn注入
                p.Use(PipelinePhase.RegistrationPipelineStart, (context, next) =>
                {
                    foreach (var dependsType in compoment.DependsOn.DependsOnLazy.Value)
                        new Autowired(false).Resolve(context, compoment.CurrentType, dependsType, dependsType.Name,
                            context.Parameters.ToList());
                    next(context);
                });
            });
        }


        
        /**
         * 遍历所有的dll 拿到
         * 所有的AutoConfiguration标签的类
         * 所有的Import标签的类
         * 所有的GetComponent标签的类
         */
        private EnumTypeAgg getAllTypeDefs()
        {
            if (_assemblyList == null || _assemblyList.Count < 1)
            {
                throw new ArgumentNullException(nameof(_assemblyList));
            }

            var enumTypeAgg = new EnumTypeAgg();
            Parallel.ForEach(_assemblyList.Where(r => !r.IsDynamic), assembly =>
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (!type.IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    var orderAttr = type.GetCustomAttribute<Order>();


                    

                    var configBean = type.GetCustomAttribute<AutoConfiguration>();
                    if (configBean != null)
                    {
                        enumTypeAgg.AutoconfigurationDefs.Add(new TypeDef<AutoConfiguration>()
                        {
                            Type = type,
                            Bean = configBean,
                            OrderIndex = orderAttr?.Index ?? configBean.OrderIndex
                        });
                    }


                    var componentAttr = type.GetComponent(ComponentDetector);
                    if (componentAttr != null)
                    {
                        enumTypeAgg.BeanDefinationDefs.Add(new BeanDefination
                        {
                            Type = type,
                            Bean = componentAttr,
                            OrderIndex = orderAttr?.Index ?? componentAttr.OrderIndex
                        });
                    }


                    var importBean = type.GetCustomAttribute<Import>();
                    if (importBean != null)
                    {
                        var beanDefinations = doImportCompnent(importBean, type);
                        beanDefinations.ForEach(enumTypeAgg.BeanDefinationDefs.Add);
                    }
                }
            });
            return enumTypeAgg;
        }
    }
}