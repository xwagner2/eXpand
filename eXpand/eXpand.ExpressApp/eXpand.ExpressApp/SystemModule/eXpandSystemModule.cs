using System.ComponentModel;
using System.Drawing;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using eXpand.ExpressApp.Core.DictionaryHelpers;

namespace eXpand.ExpressApp.SystemModule
{
    public interface IModelActionButtonDetailItem : IModelDetailViewItem
    {
        [DataSourceProperty("Application.Model.ActionDesign.Actions")]
        IModelAction ActionId { get; set; }
    }

    [ToolboxItem(true)]
    [Description("Includes Controllers that represent basic features for XAF applications.")]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [ToolboxBitmap(typeof(XafApplication), "Resources.SystemModule.ico")]
    public sealed partial class eXpandSystemModule : ModuleBase
    {
        protected override void RegisterEditorDescriptors(System.Collections.Generic.List<EditorDescriptor> editorDescriptors)
        {
            base.RegisterEditorDescriptors(editorDescriptors);
            editorDescriptors.Add(new DetailViewItemDescriptor(new DetailViewItemRegistration(typeof(IModelActionButtonDetailItem))));
        }

        public override void Setup(XafApplication application)
        {
            base.Setup(application);
            application.SetupComplete += (sender, args) => DictionaryHelper.AddFields(application.Model, application.ObjectSpaceProvider.XPDictionary);
            application.LoggedOn += (sender, args) => DictionaryHelper.AddFields(application.Model, application.ObjectSpaceProvider.XPDictionary);
        }
    }
}