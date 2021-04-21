using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ALittle
{
    public class ABnfRule
    {
        // ABnf规则内容
        private string m_buffer = "";

        // 规则集合
        private Dictionary<string, ABnfRuleInfo> m_rule_map = new Dictionary<string, ABnfRuleInfo>();

        // 关键字集合
        private HashSet<string> m_key_set = new HashSet<string>();

        // Left hand of 集合
        private HashSet<string> m_symbol_set = new HashSet<string>();

        public ABnfRule()
        {
        }

        // 初始化参数
        public void Clear()
        {
            m_buffer = "";
            m_rule_map.Clear();
            m_key_set.Clear();
            m_symbol_set.Clear();
        }

        // 加载文法
        public string Load(string buffer)
        {
            try
            {
                // 清理
                Clear();

                // 保存字符串内容
                m_buffer = buffer;

                // 解析token
                List<ABnfRuleTokenInfo> token_list = CalcToken();

                // 对token列表进行语法分析
                int offset = 0;
                while (offset < token_list.Count)
                {
                    // 解析规则
                    ABnfRuleInfo rule = CalcABnfRule(token_list, ref offset);
                    if (m_rule_map.ContainsKey(rule.id.value))
                        throw new System.Exception("Duplicate definition of rule name " + rule.id.value);
                    m_rule_map[rule.id.value] = rule;
                }

                foreach (var pair in m_rule_map)
                    pair.Value.CalcNextChar();
            }
            catch (System.Exception e)
            {
                Clear();
                return e.Message;
            }

            return null;
        }

        // 获取所有关键字
        public HashSet<string> GetKeySet()
        {
            return m_key_set;
        }

        // 获取所有Left hand of 集合
        public HashSet<string> GetSymbolSet()
        {
            return m_symbol_set;
        }

        // 根绝规则名称查找规则对象
        public ABnfRuleInfo FindRuleInfo(string id)
        {
            m_rule_map.TryGetValue(id, out ABnfRuleInfo rule);
            return rule;
        }

        // 解析规则语句
        private ABnfRuleInfo CalcABnfRule(List<ABnfRuleTokenInfo> token_list, ref int offset)
        {
            ABnfRuleInfo rule = new ABnfRuleInfo(this);

            // 跳过注释
            while (offset < token_list.Count &&
                (token_list[offset].type == ABnfRuleTokenType.TT_LINE_COMMENT
                    || token_list[offset].type == ABnfRuleTokenType.TT_BLOCK_COMMENT))
                ++offset;

            // 处理ID
            if (offset >= token_list.Count)
                throw new System.Exception("The last rule is incomplete");

            if (token_list[offset].type != ABnfRuleTokenType.TT_ID)
                throw new System.Exception("Row:" + token_list[offset].line + "Col:" + token_list[offset].col
                    + "Expected to be the rule name but got :" + token_list[offset].value);

            // 正则表达式匹配
            if (!Regex.IsMatch(token_list[offset].value, "^[_a-zA-Z][_a-zA-Z0-9]*$"))
                throw new System.Exception("Line: " + token_list[offset].line + ", Col: " + token_list[offset].col
                    + "ID must start with lowercase letters, uppercase letters, numbers, underscores, and cannot start with numbers, but the result is: " + token_list[offset].value);

            rule.id = token_list[offset];
            ++offset;

            // 处理冒号
            if (offset >= token_list.Count)
                throw new System.Exception("The last rule is incomplete");

            if (token_list[offset].type == ABnfRuleTokenType.TT_SYMBOL && token_list[offset].value == ":")
            {
                ++offset;
                // 处理预测正则表达式
                if (offset >= token_list.Count)
                    throw new System.Exception("The last rule is incomplete");

                if (token_list[offset].type != ABnfRuleTokenType.TT_REGEX)
                {
                    throw new System.Exception("Line: " + token_list[offset].line + ", Col: " + token_list[offset].col
                        + " The expectation is to predict the regular expression but got: " + token_list[offset].value);
                }

                rule.prediction = token_list[offset];
                ++offset;

                // 处理预测pin
                if (offset < token_list.Count
                    && token_list[offset].type == ABnfRuleTokenType.TT_SYMBOL
                    && token_list[offset].value == "@")
                {
                    rule.prediction_pin = ABnfRuleNodePinType.NPT_TRUE;
                    ++offset;
                }
            }

            // 处理等号
            if (offset >= token_list.Count)
                throw new System.Exception("The last rule is incomplete");

            if (token_list[offset].type != ABnfRuleTokenType.TT_SYMBOL && token_list[offset].value != "=")
            {
                throw new System.Exception("Row: " + token_list[offset].line + ", Col: " + token_list[offset].col
                    + " Expected = but got:" + token_list[offset].value);
            }
            rule.assign = token_list[offset];
            ++offset;

            // 获取规则内容
            rule.node = CalcABnfNode(token_list, ref offset);
            if (rule.node == null || rule.node.node_list.Count == 0)
                throw new System.Exception("Line: " + rule.id.line + ", Col: " + rule.id.col
                    + " Rule content is empty");

            // 如果遇到分号表示结束
            if (offset >= token_list.Count)
                throw new System.Exception("The last rule is incomplete");

            if (token_list[offset].type != ABnfRuleTokenType.TT_SYMBOL || token_list[offset].value != ";")
            {
                throw new System.Exception("Line: " + token_list[offset].line + ", Col: " + token_list[offset].col
                    + " Expected ; but got: " + token_list[offset].value);
            }
            ++offset;

            return rule;
        }

        // 解析规则节点
        private ABnfRuleNodeInfo CalcABnfNode(List<ABnfRuleTokenInfo> token_list, ref int offset)
        {
            ABnfRuleNodeInfo node = new ABnfRuleNodeInfo();

            while (offset < token_list.Count)
            {
                ABnfRuleTokenInfo token = token_list[offset];

                // 如果是注释，那么就跳过
                if (token.type == ABnfRuleTokenType.TT_LINE_COMMENT || token.type == ABnfRuleTokenType.TT_BLOCK_COMMENT)
                {
                    ++offset;
                    continue;
                }

                // 检查ID
                if (token.type == ABnfRuleTokenType.TT_ID)
                {
                    // 正则表达式匹配
                    if (!Regex.IsMatch(token.value, "^[_a-zA-Z][_a-zA-Z0-9]*$"))
                        throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                            + "ID must start with lowercase letters, uppercase letters, numbers, and underscores, and cannot start with a number, but the result is: " + token.value);

                    if (node.node_list.Count == 0)
                        node.node_list.Add(new List<ABnfRuleNodeInfo>());
                    node.node_list[node.node_list.Count - 1].Add(new ABnfRuleNodeInfo(token));

                    ++offset;
                    continue;
                }

                // 检查字符串
                if (token.type == ABnfRuleTokenType.TT_STRING
                    || token.type == ABnfRuleTokenType.TT_KEY
                    || token.type == ABnfRuleTokenType.TT_REGEX)
                {
                    if (node.node_list.Count == 0)
                        node.node_list.Add(new List<ABnfRuleNodeInfo>());
                    node.node_list[node.node_list.Count - 1].Add(new ABnfRuleNodeInfo(token));

                    ++offset;
                    continue;
                }

                // 最后检查Left hand of
                if (token.type == ABnfRuleTokenType.TT_SYMBOL)
                {
                    if (token.value == "(")
                    {
                        ++offset;

                        if (node.node_list.Count == 0)
                            node.node_list.Add(new List<ABnfRuleNodeInfo>());
                        node.node_list[node.node_list.Count - 1].Add(CalcABnfNode(token_list, ref offset));

                        if (offset >= token_list.Count || token_list[offset].value != ")")
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "The expectation is), but what I get is: " + token.value);

                        ++offset;
                        continue;
                    }
                    else if (token.value == "*")
                    {
                        if (node.node_list.Count == 0 || node.node_list[node.node_list.Count - 1].Count == 0)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "No content defined in front of the symbol * ");

                        var node_list = node.node_list[node.node_list.Count - 1];
                        if (node_list[node_list.Count - 1].repeat != ABnfRuleNodeRepeatType.NRT_NONE)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "The symbol * has previously defined the repetition rule ");

                        node_list[node_list.Count - 1].repeat = ABnfRuleNodeRepeatType.NRT_NOT_OR_MORE;

                        ++offset;
                        continue;
                    }
                    else if (token.value == "+")
                    {
                        if (node.node_list.Count == 0 || node.node_list[node.node_list.Count - 1].Count == 0)
                        {
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Symbol + no content defined in front ");
                        }

                        var node_list = node.node_list[node.node_list.Count - 1];
                        if (node_list[node_list.Count - 1].repeat != ABnfRuleNodeRepeatType.NRT_NONE)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Symbol + The repetition rule has been defined before ");

                        node_list[node_list.Count - 1].repeat = ABnfRuleNodeRepeatType.NRT_AT_LEAST_ONE;

                        ++offset;
                        continue;
                    }
                    else if (token.value == "?")
                    {
                        if (node.node_list.Count == 0 || node.node_list[node.node_list.Count - 1].Count == 0)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Symbol ? No content defined in front ");

                        var node_list = node.node_list[node.node_list.Count - 1];
                        if (node_list[node_list.Count - 1].repeat != ABnfRuleNodeRepeatType.NRT_NONE)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Symbol ? has been defined previously");

                        node_list[node_list.Count - 1].repeat = ABnfRuleNodeRepeatType.NRT_ONE_OR_NOT;

                        ++offset;
                        continue;
                    }
                    else if (token.value == "@")
                    {
                        if (node.node_list.Count == 0 || node.node_list[node.node_list.Count - 1].Count == 0)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Left hand of @ is missing");

                        var node_list = node.node_list[node.node_list.Count - 1];
                        if (node_list[node_list.Count - 1].pin != ABnfRuleNodePinType.NPT_NONE)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Symbol @ is defined after the pin rule");

                        node_list[node_list.Count - 1].pin = ABnfRuleNodePinType.NPT_TRUE;

                        ++offset;
                        continue;
                    }
                    else if (token.value == "#")
                    {
                        if (node.node_list.Count == 0 || node.node_list[node.node_list.Count - 1].Count == 0)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Left hand of # is missing");

                        var node_list = node.node_list[node.node_list.Count - 1];
                        if (node_list[node_list.Count - 1].not_key != ABnfRuleNodeNotKeyType.NNKT_NONE)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Left hand of # has been defined previously");

                        node_list[node_list.Count - 1].not_key = ABnfRuleNodeNotKeyType.NNKT_TRUE;

                        ++offset;
                        continue;
                    }
                    else if (token.value == "|")
                    {
                        if (node.node_list.Count == 0 || node.node_list[node.node_list.Count - 1].Count == 0)
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Left hand of | is missing");

                        if (offset + 1 >= token_list.Count
                            && token_list[offset + 1].type == ABnfRuleTokenType.TT_SYMBOL
                            && token_list[offset + 1].value == ";")
                        {
                            throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                                + "Right hand of | is missing");
                        }

                        node.node_list.Add(new List<ABnfRuleNodeInfo>());

                        ++offset;
                        continue;
                    }
                    else if (token.value == ";" || token.value == ")")
                    {
                        break;
                    }
                    else
                    {
                        throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                            + "Unsupported:" + token.value);
                    }
                }

                throw new System.Exception("Line: " + token.line + ", Col: " + token.col
                    + "Unknown token type:" + token.value);
            }

            return node;
        }

        // Token解析
        private List<ABnfRuleTokenInfo> CalcToken()
        {
            var token_list = new List<ABnfRuleTokenInfo>();

            int line = 0;        // 当前行
            int col = 0;         // 当前列

            ABnfRuleTokenInfo token = null;

            int index = 0;
            while (index < m_buffer.Length)
            {
                char c = m_buffer[index];
                char next_c = '\0';
                if (index + 1 < m_buffer.Length)
                    next_c = m_buffer[index + 1];

                // 计算行列
                if (c == '\n')
                {
                    ++line;
                    col = 0;
                }
                else
                {
                    ++col;
                }

                // 如果在当引号内部
                if (token != null && token.type == ABnfRuleTokenType.TT_STRING)
                {
                    if (c == '\\')
                    {
                        if (next_c == '\\' || next_c == '\'')
                        { token.value += next_c; ++index; ++col; }
                        else if (next_c == 'a')
                        { token.value += '\a'; ++index; ++col; }
                        else if (next_c == 'b')
                        { token.value += '\b'; ++index; ++col; }
                        else if (next_c == 'f')
                        { token.value += '\f'; ++index; ++col; }
                        else if (next_c == 'n')
                        { token.value += '\n'; ++index; ++col; }
                        else if (next_c == 'r')
                        { token.value += '\r'; ++index; ++col; }
                        else if (next_c == 't')
                        { token.value += '\t'; ++index; ++col; }
                        else if (next_c == 'v')
                        { token.value += '\v'; ++index; ++col; }
                        else
                            token.value += c;
                    }
                    else if (c == '\'')
                    {
                        // 收集Left hand of
                        if (!m_symbol_set.Contains(token.value))
                            m_symbol_set.Add(token.value);
                        token_list.Add(token);
                        token = null;
                    }
                    else
                    {
                        token.value += c;
                    }
                }
                else if (token != null && token.type == ABnfRuleTokenType.TT_KEY)
                {
                    if (c == '\\')
                    {
                        if (next_c == '\\' || next_c == '>')
                        { token.value += next_c; ++index; ++col; }
                        else if (next_c == 'a')
                        { token.value += '\a'; ++index; ++col; }
                        else if (next_c == 'b')
                        { token.value += '\b'; ++index; ++col; }
                        else if (next_c == 'f')
                        { token.value += '\f'; ++index; ++col; }
                        else if (next_c == 'n')
                        { token.value += '\n'; ++index; ++col; }
                        else if (next_c == 'r')
                        { token.value += '\r'; ++index; ++col; }
                        else if (next_c == 't')
                        { token.value += '\t'; ++index; ++col; }
                        else if (next_c == 'v')
                        { token.value += '\v'; ++index; ++col; }
                        else
                            token.value += c;
                    }
                    else if (c == '>')
                    {
                        // 收集关键字
                        if (!m_key_set.Contains(token.value))
                            m_key_set.Add(token.value);
                        token_list.Add(token);
                        token = null;
                    }
                    else
                    {
                        token.value += c;
                    }
                }
                else if (token != null && token.type == ABnfRuleTokenType.TT_REGEX)
                {
                    if (c == '\\')
                    {
                        if (next_c == '\\' || next_c == '"')
                        { token.value += next_c; ++index; ++col; }
                        else if (next_c == 'a')
                        { token.value += '\a'; ++index; ++col; }
                        else if (next_c == 'b')
                        { token.value += '\b'; ++index; ++col; }
                        else if (next_c == 'f')
                        { token.value += '\f'; ++index; ++col; }
                        else if (next_c == 'n')
                        { token.value += '\n'; ++index; ++col; }
                        else if (next_c == 'r')
                        { token.value += '\r'; ++index; ++col; }
                        else if (next_c == 't')
                        { token.value += '\t'; ++index; ++col; }
                        else if (next_c == 'v')
                        { token.value += '\v'; ++index; ++col; }
                        else
                            token.value += c;
                    }
                    else if (c == '"')
                    {
                        token_list.Add(token);
                        token = null;
                    }
                    else
                    {
                        token.value += c;
                    }
                }
                else if (token != null && token.type == ABnfRuleTokenType.TT_LINE_COMMENT)
                {
                    if (c == '\r')
                    {
                        if (next_c == '\n')
                        {
                            ++index;
                            ++line;
                            col = 0;
                            token_list.Add(token);
                            token = null;
                        }
                        else
                        {
                            token.value += c;
                        }
                    }
                    else if (c == '\n')
                    {
                        token_list.Add(token);
                        token = null;
                    }
                    else
                    {
                        token.value += c;
                    }
                }
                else if (token != null && token.type == ABnfRuleTokenType.TT_BLOCK_COMMENT)
                {
                    if (c == '*')
                    {
                        if (next_c == '/')
                        {
                            ++index;
                            ++col;
                            token_list.Add(token);
                            token = null;
                        }
                        else
                        {
                            token.value += c;
                        }
                    }
                    else
                    {
                        token.value += c;
                    }
                }
                else
                {
                    if (c == '\'')
                    {
                        if (token != null)
                            token_list.Add(token);
                        token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_STRING);
                        token.line = line;
                        token.col = col;
                    }
                    else if (c == '<')
                    {
                        if (token != null)
                            token_list.Add(token);
                        token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_KEY);
                        token.line = line;
                        token.col = col;
                    }
                    else if (c == '"')
                    {
                        if (token != null)
                            token_list.Add(token);
                        token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_REGEX);
                        token.line = line;
                        token.col = col;
                    }
                    else if (c == '/' && next_c == '/')
                    {
                        ++index;
                        ++col;
                        if (token != null)
                            token_list.Add(token);
                        token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_LINE_COMMENT);
                        token.line = line;
                        token.col = col;
                    }
                    else if (c == '/' && next_c == '*')
                    {
                        ++index;
                        ++col;
                        if (token != null)
                            token_list.Add(token);
                        token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_BLOCK_COMMENT);
                        token.line = line;
                        token.col = col;
                    }
                    else if (c == '=' || c == '@' || c == '#' || c == ':'
                        || c == '*' || c == '?' || c == '+'
                        || c == '|' || c == ';'
                        || c == '(' || c == ')')
                    {
                        if (token != null)
                            token_list.Add(token);

                        token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_SYMBOL);
                        token.value += c;
                        token.line = line;
                        token.col = col;
                        token_list.Add(token);

                        token = null;
                    }
                    else if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        if (token != null)
                            token_list.Add(token);

                        token = null;
                    }
                    else
                    {
                        if (token == null)
                        {
                            token = new ABnfRuleTokenInfo(ABnfRuleTokenType.TT_ID);
                            token.line = line;
                            token.col = col;
                        }
                        token.value += c;
                    }
                }

                ++index;
            }

            if (token != null)
                token_list.Add(token);

            return token_list;
        }
    }
}