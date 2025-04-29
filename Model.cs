using System.Collections.Generic;
using System.Linq;

namespace LTSpice_Lib_Merger
{
    internal class Model
    {
        public enum CompareResult
        {
            Different, //
            OtherMoreComplete,
            RefMoreComplete,
            Same
        }

        private readonly string _kind;
        private readonly List<string> _parNames;
        private readonly Dictionary<string, string> _pars;


        public Model(string model)
        {
            // We are only interested in models ...
            if (!model.ToLower().StartsWith(".model ")) return;

            var def = model.Substring(7).Trim();
            var pos = def.IndexOfAny(new[] {' ', '('});
            if (pos == -1) return;

            // Found the model name. Get name and definition
            var name = def.Substring(0, pos).ToUpper();
            def = def.Substring(pos).Trim();

            // Remove the versioning information
            name = name.TrimEnd('_');

            // Process the definition
            pos = def.IndexOfAny(new[] {' ', '(', ':'});
            if (pos == -1) return;

            // Get the model kind
            var modelKind = def.Substring(0, pos);
            def = def.Substring(pos);

            // Now remove all ( and ) and , as they are not used
            def = def.Replace('(', ' ');
            def = def.Replace(')', ' ');
            def = def.Replace(',', ' ');

            // Collapse spaces
            while (true)
            {
                var newDef = def.Replace("  ", " ");
                if (newDef == def) break;
                def = newDef;
            }

            // Collapse the equal sign
            while (true)
            {
                var newDef = def.Replace(" =", "=");
                newDef = newDef.Replace("= ", "=");
                newDef = newDef.Replace(" :", ":");
                newDef = newDef.Replace(": ", ":");

                if (newDef == def) break;
                def = newDef;
            }

            // Split model parameters
            var modelPars = def.Split(' ');
            var pars = new Dictionary<string, string>();
            var parNames = new List<string>();

            foreach (var modelPar in modelPars)
            {
                var p = modelPar.Split('=');
                var n = p[0].ToUpper(); // Model parameters will be uppercased
                pars[n] = modelPar; // We store all the definition, not only the name
                parNames.Add(n);
            }

            Name = name;
            _kind = modelKind;
            _pars = pars;
            _parNames = parNames;
            IsValid = true;
        }

        public bool IsValid { get; }

        public string Name { get; }

        /// <summary>
        ///     Get the model definition
        /// </summary>
        /// <returns></returns>
        public string Def()
        {
            var r = _kind;
            if (_kind.ToLower() != "ako")
                r += "(";
            var p = "";
            p = _parNames.Aggregate(p, (current, parName) => current + _pars[parName] + " ");
            p = p.Trim();
            r += p;
            if (_kind.ToLower() != "ako")
                r += ')';
            return r;
        }

        /// <summary>
        ///     Compare models to find out their relationship
        /// </summary>
        /// <param name="other">The OTHER model</param>
        /// <returns>The comparison result</returns>
        public CompareResult Compare(Model other)
        {
            if (Name != other.Name)
                return CompareResult.Different;

            // Lets compare kind
            if (_kind != other._kind)
                return CompareResult.Different;

            // Now, compare params, except manufacturer, that will be ignored
            var missingRef = 0;
            foreach (var par in _pars)
            {
                if (par.Key == "MFG")
                    continue;
                if (!other._pars.ContainsKey(par.Key))
                {
                    missingRef++;
                }
                else
                {
                    // Make sure params contains exactly the same text - Otherwise, models are different!
                    if (par.Value.ToLower() != other._pars[par.Key].ToLower())
                        return CompareResult.Different;
                }
            }

            var missingOther = 0;
            foreach (var par in other._pars)
            {
                if (par.Key == "MFG")
                    continue;
                if (!_pars.ContainsKey(par.Key))
                {
                    missingOther++;
                }
                else
                {
                    // Make sure params contains exactly the same text - Otherwise, models are different!
                    if (par.Value.ToLower() != _pars[par.Key].ToLower())
                        return CompareResult.Different;
                }
            }

            // If some params were not found, decide which model is more complete
            if (missingRef != 0 || missingOther != 0)
            {
                if (missingRef > missingOther) return CompareResult.OtherMoreComplete;
                if (missingRef < missingOther) return CompareResult.RefMoreComplete;
                return CompareResult.Different;
            }

            // No parameters are missing. Check the manufacturer
            if (!_pars.ContainsKey("MFG"))
                return other._pars.ContainsKey("MFG") ? CompareResult.OtherMoreComplete : CompareResult.Same;
            if (other._pars.ContainsKey("MFG"))
            {
                return _pars["MFG"].ToUpper() == other._pars["MFG"].ToUpper() ? CompareResult.Same : CompareResult.Different;
            }

            return CompareResult.RefMoreComplete;

        }
    }
}