﻿/*
 * Copyright 2017 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : Scheme Editor
 * Summary  : Main form of the application
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2017
 * Modified : 2017
 */

using Scada.Scheme.Model;
using Scada.Scheme.Model.PropertyGrid;
using Scada.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Utils;
using CM = System.ComponentModel;

namespace Scada.Scheme.Editor
{
    /// <summary>
    /// Main form of the application
    /// <para>Главная форма приложения</para>
    /// </summary>
    public partial class FrmMain : Form
    {
        /// <summary>
        /// Стартовая веб-страница редактора
        /// </summary>
        private const string StartPage = "editor.html";

        private AppData appData; // общие данные приложения
        private Editor editor;   // редактор
        private Log log;         // журнал приложения
        private Mutex mutex;     // объект для проверки запуска второй копии приложения


        /// <summary>
        /// Конструктор
        /// </summary>
        public FrmMain()
        {
            InitializeComponent();

            appData = AppData.GetAppData();
            editor = appData.Editor;
            log = appData.Log;
            mutex = null;

            Application.ThreadException += Application_ThreadException;
        }

        /// <summary>
        /// Локализовать форму
        /// </summary>
        private void LocalizeForm()
        {
            string errMsg;

            if (Localization.LoadDictionaries(appData.AppDirs.LangDir, "ScadaData", out errMsg))
                CommonPhrases.Init();
            else
                log.WriteError(errMsg);

            if (Localization.LoadDictionaries(appData.AppDirs.LangDir, "ScadaSchemeEditor", out errMsg))
            {
                Translator.TranslateForm(this, "Scada.Scheme.Editor.FrmMain");
                AppPhrases.Init();
                ofdScheme.Filter = sfdScheme.Filter = AppPhrases.SchemeFileFilter;
            }
            else
            {
                log.WriteError(errMsg);
            }
        }

        /// <summary>
        /// Локализовать атрибуты для отображения свойств компонентов
        /// </summary>
        private void LocalizeAttributes()
        {
            CM.PropertyDescriptor textPropDescr = CM.TypeDescriptor.GetProperties(typeof(StaticText))["Text"];
            CM.AttributeCollection attrs = textPropDescr.Attributes;

            DescriptionAttribute descrAttr = (DescriptionAttribute)attrs[typeof(DescriptionAttribute)];
            descrAttr.DescriptionValue = "My descr";
            /*PropertyInfo descrValProp = typeof(DescriptionAttribute).GetProperty("DescriptionValue", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            descrValProp.SetValue(descrAttr, "My custom val2", null);*/

            DisplayNameAttribute dispAttr = (DisplayNameAttribute)attrs[typeof(DisplayNameAttribute)];
            dispAttr.DisplayNameValue = "My Text";
            /*PropertyInfo dispValProp = typeof(DisplayNameAttribute).GetProperty("DisplayNameValue",
                BindingFlags.NonPublic | BindingFlags.Instance);
            dispValProp.SetValue(dispAttr, "MyDispName", null);*/

            CategoryAttribute catAttr = (CategoryAttribute)attrs[typeof(CategoryAttribute)];
            catAttr.CategoryName = "My Cat";
        }

        /// <summary>
        /// Проверить, что запущена вторая копия приложения
        /// </summary>
        private bool SecondInstanceExists()
        {
            try
            {
                bool createdNew;
                mutex = new Mutex(true, "ScadaSchemeEditorMutex", out createdNew);
                return !createdNew;
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при проверке существования второй копии приложения" :
                    "Error checking existence of a second copy of the application");
                return false;
            }
        }

        /// <summary>
        /// Открыть браузер со страницей редактора
        /// </summary>
        private void OpenBrowser()
        {
            Uri startUri = new Uri(appData.AppDirs.WebDir + StartPage);
            //Process.Start("firefox", startUri.AbsoluteUri);
            Process.Start(startUri.AbsoluteUri);
        }

        /// <summary>
        /// Подтвердить возможность закрыть схему
        /// </summary>
        private bool ConfirmCloseScheme()
        {
            if (editor.Modified)
            {
                switch (MessageBox.Show(AppPhrases.SaveSchemeConfirm, CommonPhrases.QuestionCaption,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        return true; // SaveScheme(false);
                    case DialogResult.No:
                        return true;
                    default:
                        return false;
                }
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Заполнить выпадающий список компонентов схемы
        /// </summary>
        private void FillSchemeComponents()
        {
            try
            {
                cbSchComp.BeginUpdate();
                cbSchComp.Items.Clear();

                if (editor.SchemeView != null)
                {
                    cbSchComp.Items.Add(editor.SchemeView.SchemeDocument);
                    List<BaseComponent> componentList = new List<BaseComponent>(editor.SchemeView.Components);
                    componentList.Sort();

                    foreach (BaseComponent component in componentList)
                    {
                        cbSchComp.Items.Add(component);
                    }
                }
            }
            finally
            {
                cbSchComp.EndUpdate();
            }
        }


        private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string errMsg = CommonPhrases.UnhandledException + ":\r\n" + e.Exception.Message;
            log.WriteAction(errMsg, Log.ActTypes.Exception);
            ScadaUiUtils.ShowError(errMsg);
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            // инициализация общих данных приложения
            appData.Init(Path.GetDirectoryName(Application.ExecutablePath));

            // локализация
            LocalizeForm();
            LocalizeAttributes();

            // проверка существования второй копии приложения
            if (SecondInstanceExists())
            {
                ScadaUiUtils.ShowInfo(AppPhrases.CloseSecondInstance);
                Close();
                log.WriteAction(Localization.UseRussian ?
                    "Вторая копия Редактора схем закрыта." :
                    "The second instance of Scheme Editor has been closed.");
                return;
            }

            // запуск механизма редактора схем
            if (appData.StartEditor())
            {
                // открытие браузера со страницей редактора
                OpenBrowser();
            }
            else
            {
                ScadaUiUtils.ShowInfo(string.Format(AppPhrases.FailedToStartEditor, log.FileName));
                Close();
            }
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void FrmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            // завершить работу приложения
            appData.FinalizeApp();
        }

        private void FrmMain_MouseMove(object sender, MouseEventArgs e)
        {
            // активировать форму при наведении мыши
            if (ActiveForm != this)
                BringToFront();
        }


        private void btnFileNew_Click(object sender, EventArgs e)
        {
            // создание новой схемы
            if (ConfirmCloseScheme())
            {
                editor.NewScheme();
                appData.AssignViewStamp(editor.SchemeView);
                FillSchemeComponents();
            }
        }

        private void btnFileOpen_Click(object sender, EventArgs e)
        {
            // открытие схемы из файла
            if (ConfirmCloseScheme() && ofdScheme.ShowDialog() == DialogResult.OK)
            {
                string errMsg;
                bool loadOK = editor.LoadSchemeFromFile(ofdScheme.FileName, out errMsg);
                appData.AssignViewStamp(editor.SchemeView);
                FillSchemeComponents();

                if (!loadOK)
                    ScadaUiUtils.ShowError(errMsg);
            }
        }

        private void btnFileSave_ButtonClick(object sender, EventArgs e)
        {

        }

        private void miFileSaveAs_Click(object sender, EventArgs e)
        {

        }

        private void btnFileOpenBrowser_Click(object sender, EventArgs e)
        {
            OpenBrowser();
        }

        private void btnEditCut_Click(object sender, EventArgs e)
        {
        }

        private void btnEditCopy_Click(object sender, EventArgs e)
        {

        }

        private void btnEditPaste_Click(object sender, EventArgs e)
        {

        }

        private void btnEditUndo_Click(object sender, EventArgs e)
        {

        }

        private void btnEditRedo_Click(object sender, EventArgs e)
        {

        }

        private void btnSchemePointer_Click(object sender, EventArgs e)
        {

        }

        private void btnSchemeDelete_Click(object sender, EventArgs e)
        {

        }

        private void btnHelpAbout_Click(object sender, EventArgs e)
        {
            // отображение формы о программе
            FrmAbout.ShowAbout(appData.AppDirs.ExeDir, log, this);
        }


        private void cbSchComp_SelectedIndexChanged(object sender, EventArgs e)
        {
            // отображение свойств объекта, выбранного в выпадающем списке
            propertyGrid.SelectedObject = cbSchComp.SelectedItem;
        }

        private void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            // обновление текста выпадающего списка при изменении отображаемого наименования компонента
            if (cbSchComp.SelectedItem is BaseComponent)
            {
                BaseComponent selComponent = (BaseComponent)cbSchComp.SelectedItem;
                string newDisplayName = selComponent.ToString();
                string oldDisplayName = cbSchComp.Text;

                if (oldDisplayName != newDisplayName)
                    cbSchComp.Items[cbSchComp.SelectedIndex] = selComponent;
            }
        }
    }
}
