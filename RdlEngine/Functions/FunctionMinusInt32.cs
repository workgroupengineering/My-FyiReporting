/* ====================================================================
   Copyright (C) 2004-2008  fyiReporting Software, LLC
   Copyright (C) 2011  Peter Gill <peter@majorsilence.com>

   This file is part of the fyiReporting RDL project.
	
   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.


   For additional information, email info@fyireporting.com or visit
   the website www.fyiReporting.com.
*/
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Majorsilence.Reporting.Rdl;


namespace Majorsilence.Reporting.Rdl
{
	/// <summary>
	/// Minus operator  of form lhs - rhs where both operands are decimal.
	/// </summary>
	[Serializable]
	internal class FunctionMinusInt32 : FunctionBinary, IExpr
	{

		/// <summary>
		/// Do minus on decimal data types
		/// </summary>
		public FunctionMinusInt32() 
		{
			_lhs = null;
			_rhs = null;
		}

		public FunctionMinusInt32(IExpr lhs, IExpr rhs) 
		{
			_lhs = lhs;
			_rhs = rhs;
		}

		public TypeCode GetTypeCode()
		{
			return TypeCode.Int32;
		}

		public async Task<IExpr> ConstantOptimization()
		{
			_lhs = await _lhs.ConstantOptimization();
			_rhs = await _rhs.ConstantOptimization();
			bool bLeftConst = await _lhs.IsConstant();
			bool bRightConst = await _rhs.IsConstant();
			if (bLeftConst && bRightConst)
			{
				int d = await EvaluateInt32(null, null);
				return new ConstantInteger(d);
			}
			else if (bRightConst)
			{
				int d = await _rhs.EvaluateInt32(null, null);
				if (d == 0)
					return _lhs;
			}

			return this;
		}

		// Evaluate is for interpretation  (and is relatively slow)
		public async Task<object> Evaluate(Report rpt, Row row)
		{
			return await EvaluateInt32(rpt, row);
		}
		
		public async Task<double> EvaluateDouble(Report rpt, Row row)
		{
			int result = await EvaluateInt32(rpt, row);

			return Convert.ToDouble(result);
		}

        public async Task<int> EvaluateInt32(Report rpt, Row row)
        {
            int lhs = await _lhs.EvaluateInt32(rpt, row);
            int rhs = await _rhs.EvaluateInt32(rpt, row);

            return (lhs - rhs);
        }
		
		public async Task<decimal> EvaluateDecimal(Report rpt, Row row)
		{
            int result = await EvaluateInt32(rpt, row);

            return Convert.ToDecimal(result);
        }

		public async Task<string> EvaluateString(Report rpt, Row row)
		{
			int result = await EvaluateInt32(rpt, row);
			return result.ToString();
		}

		public async Task<DateTime> EvaluateDateTime(Report rpt, Row row)
		{
			int result = await EvaluateInt32(rpt, row);
			return Convert.ToDateTime(result);
		}

		public async Task<bool> EvaluateBoolean(Report rpt, Row row)
		{
			int result = await EvaluateInt32(rpt, row);
			return Convert.ToBoolean(result);
		}
	}
}
