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
using System.Timers;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace WinRVClient
{
    public partial class ClientLogic
    {
        public static class InputValidator
        {
            /// <summary>
            /// Validates port number
            /// </summary>
            /// <param name="message">Error message when function returns false</param>
            /// <returns>Result of validation</returns>
            public static bool ValidatePortNumber(string portNumber, out string message)
            {
                message = "";
                int parsedPortNumber;

                if (Int32.TryParse(portNumber, out parsedPortNumber))
                {
                    if (parsedPortNumber > 0 && parsedPortNumber <= 65535)
                    {
                        return true;
                    }
                    else
                    {
                        message = "input number must be between 1 and 65535";
                        return false;
                    }
                }
                else
                {
                    message = "input must be integer";
                    return false;
                }
            }

            /// <summary>
            /// Validate any string EXCEPT password
            /// </summary>
            /// <param name="input">String to validate</param>
            /// <param name="message">Error message when function returns false</param>
            /// <returns>Result of validation</returns>
            public static bool ValidateString(string input, out string message)
            {
                message = "";
                int length = input.Length;
                bool[] conditions = new bool[2];

                if (input.IndexOf(':') == -1 && input.IndexOf('/') == -1)
                {
                    conditions[0] = true;
                }
                else
                {
                    message = "string cannot contain ':', '/' characters.";
                }

                if (length > 0 && length <= 32)
                {
                    conditions[1] = true;
                }
                else
                {
                    message = "string should contain between 1 and 32 characters";
                }

                return conditions[0] && conditions[1];
            }

            public static bool ValidatePassword(string input, out string message)
            {
                message = "";

                if (input.Length > 0 && input.Length <= 32)
                {
                    return true;
                }
                else
                {
                    message = "string should contain between 1 and 32 characters";
                    return false;
                }
            }
        }
    }
}
