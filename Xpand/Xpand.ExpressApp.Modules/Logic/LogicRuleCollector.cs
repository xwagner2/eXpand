﻿using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Base.Security;
using Xpand.ExpressApp.Security.Core;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.Logic;
using Xpand.Persistent.Base.Logic.Model;

namespace Xpand.ExpressApp.Logic {
    public class LogicRuleCollector {
        internal static bool PermissionsReloaded;
        ModuleBase _module;
        public event EventHandler RulesCollected;
        public event EventHandler<CollectModelLogicsArgs> CollectModelLogics;

        protected virtual void OnCollectModelLogics(CollectModelLogicsArgs e) {
            EventHandler<CollectModelLogicsArgs> handler = CollectModelLogics;
            if (handler != null) handler(this, e);
        }

        readonly Dictionary<IModelLogic, Type> _modelLogics = new Dictionary<IModelLogic, Type>(); 

        void ApplicationOnSetupComplete(object sender, EventArgs eventArgs) {
            _module.Application.SetupComplete -= ApplicationOnSetupComplete;
            CollectRules((XafApplication)sender);
        }

        protected virtual IEnumerable<ILogicRule> CollectRulesFromPermissions(IModelLogic modelLogic, ITypeInfo typeInfo) {
            return  GetPermissions().Where(permission =>permission.TypeInfo != null && permission.TypeInfo.Type == typeInfo.Type)
                .OrderBy(rule => rule.Index);
        }

        IEnumerable<IContextLogicRule> GetPermissions() {
            object user = SecuritySystem.CurrentUser as IUser;
            if (user != null) {
                return ((IUser)SecuritySystem.CurrentUser).Permissions.OfType<IContextLogicRule>();
            }
            user = SecuritySystem.CurrentUser as ISecurityUserWithRoles;
            return user != null ? ((ISecurityUserWithRoles)SecuritySystem.CurrentUser).GetPermissions().OfType<IContextLogicRule>()
                : Enumerable.Empty<IContextLogicRule>();
        }

        void ReloadPermissions() {
            if (SecuritySystem.Instance is ISecurityComplex)
                if (SecuritySystem.CurrentUser != null && !PermissionsReloaded) {
                    SecuritySystem.ReloadPermissions();
                    PermissionsReloaded = true;
                }
        }

        protected virtual ILogicRuleObject CreateRuleObject(IContextLogicRule contextLogicRule, IModelLogic modelLogic) {
            var logicRuleObjectType = LogicRuleObjectType(contextLogicRule);
            var logicRuleObject = ((ILogicRuleObject)ReflectionHelper.CreateObject(logicRuleObjectType, contextLogicRule));
            logicRuleObject.TypeInfo = contextLogicRule.TypeInfo;
            logicRuleObject.ExecutionContext = GetExecutionContext(contextLogicRule, modelLogic);
            logicRuleObject.FrameTemplateContext = GetFrameTemplateContext(contextLogicRule, modelLogic);
            AddViews(logicRuleObject.Views,modelLogic,contextLogicRule);
            return logicRuleObject;
        }

        void AddViews(HashSet<string> views, IModelLogic modelLogic, IContextLogicRule contextLogicRule) {
            var modelViewContexts = modelLogic.ViewContextsGroup.FirstOrDefault(contexts => contexts.Id == contextLogicRule.Id);
            if (modelViewContexts != null)
                foreach (var modelViewContext in modelViewContexts) {
                    views.Add(modelViewContext.Name);
                }
        }

        FrameTemplateContext GetFrameTemplateContext(IContextLogicRule contextLogicRule, IModelLogic modelLogic) {
            var templateContexts = modelLogic.FrameTemplateContextsGroup.FirstOrDefault(contexts => contexts.Id == contextLogicRule.FrameTemplateContextGroup);
            return templateContexts != null ? templateContexts.FrameTemplateContext : FrameTemplateContext.None;
        }

        ExecutionContext GetExecutionContext(IContextLogicRule contextLogicRule, IModelLogic modelLogic) {
            var modelExecutionContexts = modelLogic.ExecutionContextsGroup.FirstOrDefault(contexts => contexts.Id == contextLogicRule.ExecutionContextGroup);
            return modelExecutionContexts != null ? modelExecutionContexts.ExecutionContext : ExecutionContext.None;
        }

        Type LogicRuleObjectType(ILogicRule logicRule) {
            var typesInfo = _module.Application.TypesInfo;
            var type = _modelLogics.First(pair => pair.Value.IsInstanceOfType(logicRule)).Value;
            return typesInfo.FindTypeInfo<LogicRule>().Descendants.Single(info => !info.Type.IsAbstract&&type.IsAssignableFrom(info.Type)).Type;
        }

        void OnRulesCollected(EventArgs e) {
            EventHandler handler = RulesCollected;
            if (handler != null) handler(this, e);
        }

        public virtual void CollectRules(XafApplication xafApplication) {
            AddModelLogics();
            lock (LogicRuleManager.Instance) {
                ReloadPermissions();
                LogicRuleManager.Instance.Rules.Clear();
                foreach (var modelLogic in _modelLogics.Select(pair => pair.Key)) {
                    CollectRules(modelLogic.Rules,modelLogic);
                }
                var groupings = GetPermissions().Select(rule => new{rule,Type=rule.GetType()}).
                    GroupBy(arg => arg.Type).Select(grouping 
                        => new { ModelLogic = GetModelLogic(grouping.Key), Rules = grouping.Select(arg => arg.rule) });
                foreach (var grouping in groupings) {
                    CollectRules(grouping.Rules, grouping.ModelLogic);
                }
            }
            OnRulesCollected(EventArgs.Empty);
        }

        protected virtual void CollectRules(IEnumerable<IContextLogicRule> logicRules, IModelLogic modelLogic) {
            var ruleObjects = logicRules.Select(rule => CreateRuleObject(rule, modelLogic));
            var groupings = ruleObjects.GroupBy(rule => rule.TypeInfo).Select(grouping => new { grouping.Key, Rules = grouping });
            foreach (var grouping in groupings) {
                var typeInfo = grouping.Key;
                var rules = LogicRuleManager.Instance[typeInfo];
                IGrouping<ITypeInfo, ILogicRuleObject> collection = grouping.Rules;
                rules.AddRange(collection);
                foreach (var info in typeInfo.Descendants) {
                    LogicRuleManager.Instance[info].AddRange(collection);
                }
            }
        }

        IModelLogic GetModelLogic(Type type) {
            return _modelLogics.First(pair => pair.Value.IsAssignableFrom(type)).Key;
        }

        void AddModelLogics() {
            var collectModelLogicsArgs = new CollectModelLogicsArgs();
            OnCollectModelLogics(collectModelLogicsArgs);
            var modelLogics = collectModelLogicsArgs.ModelLogics.Where(modelLogic => !_modelLogics.ContainsKey(modelLogic));
            var typesInfo = _module.Application.TypesInfo;
            foreach (var modelLogic in modelLogics) {
                var ruleType = typesInfo.FindTypeInfo(modelLogic.GetType()).ImplementedInterfaces.Where(info 
                    => typeof (IModelLogic).IsAssignableFrom(info.Type)).Select(info 
                        => info.FindAttribute<ModelLogicValidRuleAttribute>()).First().RuleType;
                _modelLogics.Add(modelLogic, ruleType);
            }
        }

        public void Attach(ModuleBase moduleBase) {
            _module = moduleBase;
            if (moduleBase.Application != null) {
                moduleBase.Application.LoggedOn += (o, eventArgs) => CollectRules((XafApplication)o);
                moduleBase.Application.SetupComplete += ApplicationOnSetupComplete;
            }
        }
    }

    public class CollectModelLogicsArgs : EventArgs {
        readonly List<IModelLogic> _modelLogics=new List<IModelLogic>();
        public List<IModelLogic> ModelLogics {
            get { return _modelLogics; }
        }
    }
}