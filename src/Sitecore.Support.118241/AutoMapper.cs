using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Data;
using Sitecore.Forms.Mvc.Helpers;
using Sitecore.Forms.Mvc.Interfaces;
using Sitecore.Forms.Mvc.Reflection;
using Sitecore.Forms.Mvc.ViewModels;
using Sitecore.Mvc.Extensions;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sitecore.Support.Forms.Mvc.Services
{
    public class AutoMapper : IAutoMapper<IFormModel, FormViewModel>
    {
        public FormViewModel GetView(IFormModel formModel)
        {
            Assert.ArgumentNotNull((object)formModel, "formModel");
            FormViewModel formViewModel = new FormViewModel()
            {
                UniqueId = formModel.UniqueId,
                Information = formModel.Item.Introduction ?? string.Empty,
                IsAjaxForm = formModel.Item.IsAjaxMvcForm,
                IsSaveFormDataToStorage = formModel.Item.IsSaveFormDataToStorage,
                Title = formModel.Item.FormName ?? string.Empty,
                Name = formModel.Item.FormName ?? string.Empty,
                TitleTag = formModel.Item.TitleTag.ToString(),
                ShowTitle = formModel.Item.ShowTitle,
                ShowFooter = formModel.Item.ShowFooter,
                ShowInformation = formModel.Item.ShowIntroduction,
                SubmitButtonName = formModel.Item.SubmitName ?? string.Empty,
                SubmitButtonPosition = formModel.Item.SubmitButtonPosition ?? string.Empty,
                SubmitButtonSize = formModel.Item.SubmitButtonSize ?? string.Empty,
                SubmitButtonType = formModel.Item.SubmitButtonType ?? string.Empty,
                SuccessMessage = formModel.Item.SuccessMessage ?? string.Empty,
                SuccessSubmit = false,
                Errors = formModel.Failures.Select<ExecuteResult.Failure, string>((Func<ExecuteResult.Failure, string>)(x => x.ErrorMessage)).ToList<string>(),
                Visible = true,
                LeftColumnStyle = formModel.Item.LeftColumnStyle,
                RightColumnStyle = formModel.Item.RightColumnStyle,
                Footer = formModel.Item.Footer,
                Item = formModel.Item.InnerItem,
                FormType = formModel.Item.FormType,
                ReadQueryString = formModel.ReadQueryString,
                QueryParameters = formModel.QueryParameters
            };
            formViewModel.CssClass = ((formModel.Item.FormTypeClass ?? string.Empty) + " " + (formModel.Item.CustomCss ?? string.Empty) + " " + (formModel.Item.FormAlignment ?? string.Empty)).Trim();
            ReflectionUtils.SetXmlProperties((object)formViewModel, formModel.Item.Parameters, true);
            formViewModel.Sections = ((IEnumerable<Item>)formModel.Item.SectionItems).Select<Item, SectionViewModel>((Func<Item, SectionViewModel>)(x => this.GetSectionViewModel(new SectionItem(x), formViewModel))).Where<SectionViewModel>((Func<SectionViewModel, bool>)(x => x != null)).ToList<SectionViewModel>();
            return formViewModel;
        }

        public void SetModelResults(FormViewModel view, IFormModel formModel)
        {
            //Assert.ArgumentNotNull((object)view, "view");
            //Assert.ArgumentNotNull((object)formModel, "formModel");
            //formModel.Results = view.Sections.SelectMany<SectionViewModel, FieldViewModel>((Func<SectionViewModel, IEnumerable<FieldViewModel>>)(x => (IEnumerable<FieldViewModel>)x.Fields)).Select<FieldViewModel, ControlResult>((Func<FieldViewModel, ControlResult>)(x => ((IFieldResult)x).GetResult())).Where<ControlResult>((Func<ControlResult, bool>)(x =>
            //{
            //    if (x != null)
            //        return x.Value != null;
            //    return false;
            //})).ToList<ControlResult>();
            Sitecore.Diagnostics.Assert.ArgumentNotNull(view, "view");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(formModel, "formModel");

            foreach (var section in view.Sections)
            {
                foreach (var field in section.Fields)
                {
                    formModel.Results.Add(((IFieldResult)field).GetResult());
                }
            }

            var results = view.Sections.SelectMany(x => x.Fields).Select(x => ((IFieldResult)x).GetResult()).Where(x => x != null);
            foreach (var result in results)
            {
                if (result.Value == null) result.Value = string.Empty;
            }

            formModel.Results = results.ToList();

            foreach (var formModelResult in formModel.Results)
            {
                if (formModelResult.Value == null) formModelResult.Value = string.Empty;
            }
        }

        protected SectionViewModel GetSectionViewModel(SectionItem item, FormViewModel formViewModel)
        {
            Assert.ArgumentNotNull((object)item, "item");
            Assert.ArgumentNotNull((object)formViewModel, "formViewModel");
            SectionViewModel sectionViewModel = new SectionViewModel()
            {
                Fields = new List<FieldViewModel>(),
                Item = item.InnerItem
            };
            string title = item.Title;
            sectionViewModel.Visible = true;
            if (!string.IsNullOrEmpty(title))
            {
                sectionViewModel.ShowInformation = true;
                sectionViewModel.Title = item.Title ?? string.Empty;
                ReflectionUtils.SetXmlProperties((object)sectionViewModel, item.Parameters, true);
                sectionViewModel.ShowTitle = sectionViewModel.ShowLegend != "No";
                ReflectionUtils.SetXmlProperties((object)sectionViewModel, item.LocalizedParameters, true);
            }
            sectionViewModel.Fields = ((IEnumerable<FieldItem>)item.Fields).Select<FieldItem, FieldViewModel>((Func<FieldItem, FieldViewModel>)(x => this.GetFieldViewModel((IFieldItem)x, formViewModel))).Where<FieldViewModel>((Func<FieldViewModel, bool>)(x => x != null)).ToList<FieldViewModel>();
            if (!string.IsNullOrEmpty(item.Conditions))
                RulesManager.RunRules(item.Conditions, (object)sectionViewModel);
            if (sectionViewModel.Visible)
                return sectionViewModel;
            return (SectionViewModel)null;
        }

        protected FieldViewModel GetFieldViewModel(IFieldItem item, FormViewModel formViewModel)
        {
            Assert.ArgumentNotNull((object)item, "item");
            Assert.ArgumentNotNull((object)formViewModel, "formViewModel");
            string mvcClass = item.MVCClass;
            if (string.IsNullOrEmpty(mvcClass))
                return new FieldViewModel()
                {
                    Item = item.InnerItem
                };
            Type type = Type.GetType(mvcClass);
            if (type == (Type)null)
                return new FieldViewModel()
                {
                    Item = item.InnerItem
                };
            object instance = Activator.CreateInstance(type);
            FieldViewModel fieldViewModel = instance as FieldViewModel;
            if (fieldViewModel == null)
            {
                Log.Warn(string.Format("[WFFM]Unable to create instance of type {0}", (object)mvcClass), (object)this);
                return (FieldViewModel)null;
            }
            fieldViewModel.Title = item.Title ?? string.Empty;
            fieldViewModel.Name = item.Name ?? string.Empty;
            fieldViewModel.Visible = true;
            if (fieldViewModel != null)
                fieldViewModel.IsRequired = item.IsRequired;
            fieldViewModel.ShowTitle = true;
            fieldViewModel.Item = item.InnerItem;
            fieldViewModel.FormId = formViewModel.Item.ID.ToString();
            fieldViewModel.FormType = formViewModel.FormType;
            fieldViewModel.FieldItemId = item.ID.ToString();
            fieldViewModel.LeftColumnStyle = formViewModel.LeftColumnStyle;
            fieldViewModel.RightColumnStyle = formViewModel.RightColumnStyle;
            fieldViewModel.ShowInformation = true;
            Dictionary<string, string> parametersDictionary = item.ParametersDictionary;
            parametersDictionary.AddRange<string, string>(item.LocalizedParametersDictionary);
            fieldViewModel.Parameters = parametersDictionary;
            ReflectionUtil.SetXmlProperties(instance, item.ParametersDictionary);
            ReflectionUtil.SetXmlProperties(instance, item.LocalizedParametersDictionary);
            fieldViewModel.Parameters.AddRange<string, string>(item.MvcValidationMessages);
            if (!fieldViewModel.Visible)
                return (FieldViewModel)null;
            fieldViewModel.Initialize();
            if (!string.IsNullOrEmpty(item.Conditions))
                RulesManager.RunRules(item.Conditions, (object)fieldViewModel);
            if (formViewModel.ReadQueryString && formViewModel.QueryParameters != null && !string.IsNullOrEmpty(formViewModel.QueryParameters[fieldViewModel.Title]))
            {
                MethodInfo method = fieldViewModel.GetType().GetMethod("SetValueFromQuery");
                if (method != (MethodInfo)null)
                    method.Invoke((object)fieldViewModel, new object[1]
                    {
            (object) formViewModel.QueryParameters[fieldViewModel.Title]
                    });
            }
            return fieldViewModel;
        }
    }
}
