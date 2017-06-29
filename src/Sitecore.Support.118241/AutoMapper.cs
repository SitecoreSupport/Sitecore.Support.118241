using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Data;
using Sitecore.Forms.Mvc.Helpers;
using Sitecore.Forms.Mvc.Interfaces;
using Sitecore.Forms.Mvc.Models;
using Sitecore.Forms.Mvc.Reflection;
using Sitecore.Forms.Mvc.ViewModels;
using Sitecore.Mvc.Extensions;
using Sitecore.WFFM.Abstractions.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sitecore.Support.Forms.Mvc.Services
{
    public class AutoMapper : IAutoMapper<FormModel, FormViewModel>
    {
        public FormViewModel GetView(FormModel modelEntity)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(modelEntity, "modelEntity");
            FormViewModel formViewModel = new FormViewModel();
            formViewModel.UniqueId = modelEntity.UniqueId;
            formViewModel.Information = (modelEntity.Item.Introduction ?? string.Empty);
            formViewModel.IsAjaxForm = modelEntity.Item.IsAjaxMvcForm;
            formViewModel.IsSaveFormDataToStorage = modelEntity.Item.IsSaveFormDataToStorage;
            formViewModel.Title = (modelEntity.Item.FormName ?? string.Empty);
            formViewModel.TitleTag = modelEntity.Item.TitleTag.ToString();
            formViewModel.ShowTitle = modelEntity.Item.ShowTitle;
            formViewModel.ShowFooter = modelEntity.Item.ShowFooter;
            formViewModel.ShowInformation = modelEntity.Item.ShowIntroduction;
            formViewModel.SubmitButtonName = (modelEntity.Item.SubmitName ?? string.Empty);
            formViewModel.SubmitButtonPosition = (modelEntity.Item.SubmitButtonPosition ?? string.Empty);
            formViewModel.SubmitButtonSize = (modelEntity.Item.SubmitButtonSize ?? string.Empty);
            formViewModel.SubmitButtonType = (modelEntity.Item.SubmitButtonType ?? string.Empty);
            formViewModel.SuccessMessage = (modelEntity.Item.SuccessMessage ?? string.Empty);
            formViewModel.SuccessSubmit = false;
            formViewModel.Errors = (from x in modelEntity.Failures
                                    select x.ErrorMessage).ToList<string>();
            formViewModel.RedirectUrl = modelEntity.SuccessRedirectUrl;
            formViewModel.Visible = true;
            formViewModel.LeftColumnStyle = modelEntity.Item.LeftColumnStyle;
            formViewModel.RightColumnStyle = modelEntity.Item.RightColumnStyle;
            formViewModel.Footer = modelEntity.Item.Footer;
            formViewModel.Item = modelEntity.Item.InnerItem;
            formViewModel.FormType = modelEntity.Item.FormType;
            formViewModel.ReadQueryString = modelEntity.ReadQueryString;
            formViewModel.QueryParameters = modelEntity.QueryParameters;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(modelEntity.Item.FormTypeClass ?? string.Empty).Append(" ").Append(modelEntity.Item.CustomCss ?? string.Empty).Append(" ").Append(modelEntity.Item.FormAlignment ?? string.Empty);
            formViewModel.CssClass = stringBuilder.ToString().Trim();
            ReflectionUtils.SetXmlProperties(formViewModel, modelEntity.Item.Parameters, true);
            formViewModel.Sections = (from x in modelEntity.Item.SectionItems
                                      select this.GetSectionViewModel(new SectionItem(x), formViewModel) into x
                                      where x != null
                                      select x).ToList<SectionViewModel>();
            return formViewModel;
        }

        public void SetModelResults(FormViewModel view, FormModel formModel)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(view, "view");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(formModel, "formModel");

            var results = view.Sections.SelectMany(x => x.Fields).Select(x => ((IFieldResult)x).GetResult()).Where(x => x != null).ToList();

            foreach (var result in results)
            {
                if (result.Value == null) result.Value = string.Empty;
            }

            formModel.Results = results;
        }

        protected SectionViewModel GetSectionViewModel([NotNull] SectionItem item, FormViewModel formViewModel)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(item, "item");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(formViewModel, "formViewModel");
            SectionViewModel sectionViewModel = new SectionViewModel();
            sectionViewModel.Fields = new List<FieldViewModel>();
            sectionViewModel.Item = item.InnerItem;
            string title = item.Title;
            sectionViewModel.Visible = true;
            if (!string.IsNullOrEmpty(title))
            {
                sectionViewModel.ShowInformation = true;
                sectionViewModel.Title = (item.Title ?? string.Empty);
                ReflectionUtils.SetXmlProperties(sectionViewModel, item.Parameters, true);
                sectionViewModel.ShowTitle = (sectionViewModel.ShowLegend != "No");
                ReflectionUtils.SetXmlProperties(sectionViewModel, item.LocalizedParameters, true);
            }
            sectionViewModel.Fields = (from x in item.Fields
                                       select this.GetFieldViewModel(x, formViewModel) into x
                                       where x != null
                                       select x).ToList<FieldViewModel>();
            if (!string.IsNullOrEmpty(item.Conditions))
            {
                RulesManager.RunRules(item.Conditions, sectionViewModel);
            }
            if (sectionViewModel.Visible)
            {
                return sectionViewModel;
            }
            return null;
        }

        [CanBeNull]
        protected FieldViewModel GetFieldViewModel([NotNull] IFieldItem item, FormViewModel formViewModel)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(item, "item");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(formViewModel, "formViewModel");
            string mVCClass = item.MVCClass;
            if (string.IsNullOrEmpty(mVCClass))
            {
                return new FieldViewModel
                {
                    Item = item.InnerItem
                };
            }
            Type type = Type.GetType(mVCClass);
            if (type == null)
            {
                return new FieldViewModel
                {
                    Item = item.InnerItem
                };
            }
            object obj = Activator.CreateInstance(type);
            FieldViewModel fieldViewModel = obj as FieldViewModel;
            if (fieldViewModel == null)
            {
                Sitecore.Diagnostics.Log.Warn(string.Format("[WFFM]Unable to create instance of type {0}", mVCClass), this);
                return null;
            }
            fieldViewModel.Title = (item.Title ?? string.Empty);
            fieldViewModel.Visible = true;
            if (fieldViewModel != null)
            {
                ((IHasIsRequired)fieldViewModel).IsRequired = item.IsRequired;
            }
            fieldViewModel.ShowTitle = true;
            fieldViewModel.Item = item.InnerItem;
            fieldViewModel.FormId = formViewModel.Item.ID.ToString();
            fieldViewModel.FormType = formViewModel.FormType;
            fieldViewModel.FieldItemId = item.ID.ToString();
            fieldViewModel.LeftColumnStyle = formViewModel.LeftColumnStyle;
            fieldViewModel.RightColumnStyle = formViewModel.RightColumnStyle;
            fieldViewModel.ShowInformation = true;
            Dictionary<string, string> parametersDictionary = item.ParametersDictionary;
            parametersDictionary.AddRange(item.LocalizedParametersDictionary);
            fieldViewModel.Parameters = parametersDictionary;
            ReflectionUtil.SetXmlProperties(obj, item.ParametersDictionary);
            ReflectionUtil.SetXmlProperties(obj, item.LocalizedParametersDictionary);
            fieldViewModel.Parameters.AddRange(item.MvcValidationMessages);
            if (!string.IsNullOrEmpty(item.Conditions))
            {
                RulesManager.RunRules(item.Conditions, fieldViewModel);
            }
            if (!fieldViewModel.Visible)
            {
                return null;
            }
            fieldViewModel.Initialize();
            if (formViewModel.ReadQueryString && formViewModel.QueryParameters != null && !string.IsNullOrEmpty(formViewModel.QueryParameters[fieldViewModel.Title]))
            {
                MethodInfo method = fieldViewModel.GetType().GetMethod("SetValueFromQuery");
                if (method != null)
                {
                    method.Invoke(fieldViewModel, new object[]
                    {
                        formViewModel.QueryParameters[fieldViewModel.Title]
                    });
                }
            }
            return fieldViewModel;
        }
    }
}