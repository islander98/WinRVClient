// Copyright Piotr Trojanowski 2015

// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2.1 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WinAppStock
{
    /// <summary>
    /// Provides basic handling for stocks and accessing its files.
    /// </summary>
    public class BaseStock
    {
        /// <summary>
        /// Name of the stock (equal to the directory name)
        /// </summary>
        public string name { get; protected set; }

        /// <summary>
        /// Directory path to this stock
        /// </summary>
        public string stockPath { get; protected set; }

        /// <summary>
        /// Informs if the stock was reused or new directory was created
        /// </summary>
        public bool isStockReused { get; protected set; }

        /// <summary>
        /// Informs if the object refers to the stock which was permanently removed
        /// </summary>
        /// <remarks>
        /// Calling any methods on previously removed stock causes an exception
        /// to be thrown.
        /// </remarks>
        public bool isRemoved { get; protected set; }
        
        /// <summary>
        /// Get reference to the child stock. Every method call creates a new object 
        /// even if it references the same directory.
        /// </summary>
        /// <param name="name">name of the child stock</param>
        /// <returns>a BaseStock object representing the child stock of a given name</returns>
        public virtual BaseStock GetChildStockRef(string name)
        {
            if (this.isRemoved)
            {
                throw new Exception("Cannot perform operation on a removed stock: \"" + name + "\".");
            }

            return new BaseStock(name, this.stockPath + "\\" + name);
        }

        /// <summary>
        /// Delegate of a function to initialize newly created file
        /// </summary>
        /// <param name="stream">stream corresponding to the opened file</param>
        /// <see cref="GetChildFile(string, Initializer)"/>
        public delegate void Initializer(FileStream stream);

        /// <summary>
        /// Returns the FileStream corresponding to the child file in this stock
        /// </summary>
        /// <param name="name">name of the file</param>
        /// <param name="func">function called to initialize the file if it is created for the first time</param>
        /// <returns>Stream corresponding to the child file</returns>
        public virtual FileStream GetChildFile(string name, Initializer func)
        {
            if (this.isRemoved)
            {
                throw new Exception("Cannot perform operation on a removed stock: \"" + name + "\".");
            }

            if (!isFilenameAllowed(name))
            {
                throw new Exception("Invalid filename: \"" + name + "\".");
            }

            string fullFilePath = this.stockPath + "\\" + name;
            bool fileExists = File.Exists(fullFilePath);
            FileStream openedFile = null;

            try
            {
                 openedFile = File.Open(fullFilePath, FileMode.OpenOrCreate);

                // if the file was created, initialize it with the provided initializer
                if (!fileExists && func != null)
                {
                    func(openedFile);
                    openedFile.Flush();
                    openedFile.Seek(0, SeekOrigin.Begin);
                }

                return openedFile;
            }
            catch (Exception e)
            {
                if (openedFile != null)
                {
                    // so the file was created but something went wrong during
                    // initialization; we should remove it as it may contain 
                    // possibly broken content
                    openedFile.Close();
                    try
                    {
                        File.Delete(fullFilePath);
                    }
                    catch
                    {
                        // if delete fails we can't do much.
                    }
                }

                throw new Exception("Could not open/read file: \"" + fullFilePath + "\".", e);
            }
        }

        /// <summary>
        /// Returns the FileStream corresponding to the child file in this stock
        /// </summary>
        /// <param name="name">name of the file</param>
        /// <returns>Stream corresponding to the child file</returns>
        public virtual FileStream GetChildFile(string name)
        {
            return this.GetChildFile(name, null);
        }

        /// <summary>
        /// Deletes the stock. It makes any references to its child invalid.
        /// </summary>
        /// <remarks>
        /// Performing any operations on invalid children has unexpected behavior.
        /// </remarks>
        public virtual void DeleteStock()
        {
            this.isRemoved = true;

            try
            {
                Directory.Delete(this.stockPath, true);
            }
            catch (Exception e)
            {
                throw new Exception("Error during stock removal: \"" + this.stockPath + "\".", e);
            }
        }

        /// <summary>
        /// Deletes the file. It makes any FileStream referring to this file invalid.
        /// </summary>
        /// <param name="name">Name of the child to remove.</param>
        public virtual void DeleteChildFile(string name)
        {
            if (!isFilenameAllowed(name))
            {
                throw new Exception("Invalid filename: \"" + name + "\".");
            }

            try
            {
                File.Delete(this.stockPath + "\\" + name);
            }
            catch (Exception e)
            {
                throw new Exception("Error during child file removal: \"" + this.stockPath + "\\" + name + "\".", e);
            }
        }

        /// <summary>
        /// Checks for disallowed filename characters in given name
        /// </summary>
        /// <param name="name">string to check</param>
        /// <returns>true if name is allowed, false otherwise</returns>
        private static bool isFilenameAllowed(string name)
        {
            char[] chars = Path.GetInvalidFileNameChars();
            bool ret = true;

            foreach (char c in name)
            {
                foreach (char d in chars)
                {
                    if (c == d)
                    {
                        ret = false;
                        break;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Initialize the new stock by creating the directory (if it doesn't exists yet)
        /// </summary>
        protected virtual void InitializeNewStock()
        {
            if (!Directory.Exists(this.stockPath))
            {
                try
                {
                    Directory.CreateDirectory(this.stockPath);
                }
                catch (Exception e)
                {
                    throw new Exception("Could not create directory: \"" + this.stockPath + "\".", e);
                }
                this.isStockReused = false;
            }
            else
            {
                this.isStockReused = true;
            }
        }

        /// <summary>
        /// Initializes the stock object with a given name. It may cause 
        /// creating a new stock directory with a given name or reuse the
        /// existing one.
        /// </summary>
        /// <see cref="isStockReused"/>
        /// <param name="name">Name of the stock. If it contains any characters 
        /// disallowed in the directory name (including '\') an exception is 
        /// thrown.</param>
        /// <remarks> To create multilevel stock, you need to create a top level 
        /// WinAppStock and add child stocks. That will result in creating 
        /// subdirectories reflecting the stock tree.</remarks>
        public BaseStock(string name)
        {
            if (!isFilenameAllowed(name))
            {
                throw new Exception("Invalid filename: \"" + name + "\".");
            }

            this.name = name;
            this.stockPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + name;
            this.isRemoved = false;

            this.InitializeNewStock();
        }

        /// <summary>
        /// Does the same as public constructor but allows to provide the absolute path.
        /// The name is not checked against invalid characters.
        /// </summary>
        /// <param name="name">name of the stock</param>
        /// <param name="absolutePath">absolute path of the stock directory to create 
        /// (includies the directory name)</param>
        /// <see cref="BaseStock(string)"/>
        protected BaseStock(string name, string absolutePath)
        {
            this.name = name;
            this.stockPath = absolutePath + "\\" + name;
            this.isRemoved = false;

            this.InitializeNewStock();
        }
    }
}
