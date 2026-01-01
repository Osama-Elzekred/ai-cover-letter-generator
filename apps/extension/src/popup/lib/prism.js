/**
 * Prism: Lightweight, robust, elegant syntax highlighting
 * MIT license http://www.opensource.org/licenses/mit-license.php/
 * @author Lea Verou http://lea.verou.me
 */

var Prism = (function(_self){

var lang = /\blang(?:uage)?-([\w-]+)\b/i;
var uniqueId = 0;

var _ = _self.Prism = {
	manual: _self.Prism && _self.Prism.manual,
	disableWorkerAppend: _self.Prism && _self.Prism.disableWorkerAppend,
	util: {
		encode: function encode(tokens) {
			if (tokens instanceof Token) {
				return new Token(tokens.type, encode(tokens.content), tokens.alias);
			} else if (Array.isArray(tokens)) {
				return tokens.map(encode);
			} else {
				return tokens.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/\u00a0/g, ' ');
			}
		},

		type: function (o) {
			return Object.prototype.toString.call(o).slice(8, -1);
		},

		objId: function (obj) {
			if (!obj['__id']) {
				Object.defineProperty(obj, '__id', { value: ++uniqueId });
			}
			return obj['__id'];
		},

		clone: function deepClone(o, visited) {
			var clone, id, type = _.util.type(o);
			visited = visited || {};

			switch (type) {
				case 'Object':
					id = _.util.objId(o);
					if (visited[id]) {
						return visited[id];
					}
					clone = {};
					visited[id] = clone;

					for (var key in o) {
						if (o.hasOwnProperty(key)) {
							clone[key] = deepClone(o[key], visited);
						}
					}

					return clone;

				case 'Array':
					id = _.util.objId(o);
					if (visited[id]) {
						return visited[id];
					}
					clone = [];
					visited[id] = clone;

					o.forEach(function (v, i) {
						clone[i] = deepClone(v, visited);
					});

					return clone;

				default:
					return o;
			}
		},

		getLanguage: function (element) {
			while (element) {
				var m = lang.exec(element.className);
				if (m) {
					return m[1].toLowerCase();
				}
				element = element.parentElement;
			}
			return 'none';
		},

		setLanguage: function (element, language) {
			element.className = element.className.replace(lang, '');
			element.classList.add('language-' + language);
		}
	},

	languages: {
		plain: {},
		plaintext: {},
		text: {},
		txt: {},
		extend: function (id, redef) {
			var lang = _.util.clone(_.languages[id]);

			for (var key in redef) {
				lang[key] = redef[key];
			}

			return lang;
		},

		/**
		 * Insert a token before another token in a language definition
		 * @param {string} inside The property of `root` that contains the object to be modified
		 * @param {string} before The key to insert before
		 * @param {Object} insert The new tokens to insert
		 * @param {Object} [root] The object containing `inside`
		 * @returns {Object} The new language definition in `root[inside]`
		 */
		insertBefore: function (inside, before, insert, root) {
			root = root || _.languages;
			var grammar = root[inside];
			var ret = {};

			for (var token in grammar) {
				if (grammar.hasOwnProperty(token)) {

					if (token == before) {
						for (var newNode in insert) {
							if (insert.hasOwnProperty(newNode)) {
								ret[newNode] = insert[newNode];
							}
						}
					}

					// Do not insert token which also occur in insert. See #1525
					if (!insert.hasOwnProperty(token)) {
						ret[token] = grammar[token];
					}
				}
			}

			var old = root[inside];
			root[inside] = ret;

			// Update references in other language definitions
			_.languages.DFS(_.languages, function(key, value) {
				if (value === old && key != inside) {
					this[key] = ret;
				}
			});

			return ret;
		},

		// Traverse a language definition with Depth First Search
		DFS: function DFS(o, callback, type, visited) {
			visited = visited || {};
			var objId = _.util.objId;

			for (var i in o) {
				if (o.hasOwnProperty(i)) {
					callback.call(o, i, o[i], type || i);

					var property = o[i],
					    propertyType = _.util.type(property);

					if (propertyType === 'Object' && !visited[objId(property)]) {
						visited[objId(property)] = true;
						DFS(property, callback, null, visited);
					}
					else if (propertyType === 'Array' && !visited[objId(property)]) {
						visited[objId(property)] = true;
						DFS(property, callback, i, visited);
					}
				}
			}
		}
	},

	plugins: {},

	highlightAll: function(async, callback) {
		_.highlightAllUnder(document, async, callback);
	},

	highlightAllUnder: function(container, async, callback) {
		var env = {
			callback: callback,
			container: container,
			selector: 'code[class*="language-"], [class*="language-"] code, code[class*="lang-"], [class*="lang-"] code'
		};

		_.hooks.run('before-highlightall', env);

		env.elements = Array.prototype.slice.call(env.container.querySelectorAll(env.selector));

		_.hooks.run('before-all-elements-highlight', env);

		for (var i=0, element; element = env.elements[i++];) {
			_.highlightElement(element, async === true, env.callback);
		}
	},

	highlightElement: function(element, async, callback) {
		// Find language
		var language = _.util.getLanguage(element);
		var grammar = _.languages[language];

		// Set language on the element, if not present
		_.util.setLanguage(element, language);

		// Set language on the parent, for styling
		var parent = element.parentElement;
		if (parent && parent.nodeName.toLowerCase() === 'pre') {
			_.util.setLanguage(parent, language);
		}

		var code = element.textContent;

		var env = {
			element: element,
			language: language,
			grammar: grammar,
			code: code
		};

		function insertHighlightedCode(highlightedCode) {
			env.highlightedCode = highlightedCode;

			_.hooks.run('before-insert', env);

			env.element.innerHTML = env.highlightedCode;

			_.hooks.run('after-highlight', env);
			_.hooks.run('complete', env);
			callback && callback.call(env.element);
		}

		_.hooks.run('before-sanity-check', env);

		// Inject any plugins that might modify the environment
		parent = env.element.parentElement;
		if (parent && parent.nodeName.toLowerCase() === 'pre' && !parent.hasAttribute('tabindex')) {
			parent.setAttribute('tabindex', '0');
		}

		if (!env.code) {
			_.hooks.run('complete', env);
			callback && callback.call(env.element);
			return;
		}

		_.hooks.run('before-highlight', env);

		if (!env.grammar) {
			insertHighlightedCode(_.util.encode(env.code));
			return;
		}

		insertHighlightedCode(_.highlight(env.code, env.grammar, env.language));
	},

	highlight: function (code, grammar, language) {
		var env = {
			code: code,
			grammar: grammar,
			language: language
		};
		_.hooks.run('before-tokenize', env);
		env.tokens = _.tokenize(env.code, env.grammar);
		_.hooks.run('after-tokenize', env);
		return Token.stringify(_.util.encode(env.tokens), env.language);
	},

	tokenize: function(code, grammar) {
		var rest = grammar.rest;
		if (rest) {
			for (var token in rest) {
				grammar[token] = rest[token];
			}

			delete grammar.rest;
		}

		var tokenList = new LinkedList();
		addAfter(tokenList, tokenList.head, code);

		tokenizeXML(tokenList, code, grammar, tokenList.head, 0);

		return toArray(tokenList);
	},

	hooks: {
		all: {},

		add: function (name, callback) {
			var hooks = _.hooks.all;

			hooks[name] = hooks[name] || [];

			hooks[name].push(callback);
		},

		run: function (name, env) {
			var callbacks = _.hooks.all[name];

			if (callbacks && callbacks.length) {
				for (var i=0, callback; callback = callbacks[i++];) {
					callback(env);
				}
			}
		}
	},

	Token: Token
};

_self.Prism = _;

function Token(type, content, alias, matchedStr) {
	this.type = type;
	this.content = content;
	this.alias = alias;
	// Copy of the full string this token was created from
	this.length = (matchedStr || "").length | 0;
}

Token.stringify = function stringify(o, language) {
	if (typeof o == 'string') {
		return o;
	}

	if (Array.isArray(o)) {
		var s = '';
		o.forEach(function (e) {
			s += stringify(e, language);
		});
		return s;
	}

	var env = {
		type: o.type,
		content: stringify(o.content, language),
		tag: 'span',
		classes: ['token', o.type],
		attributes: {},
		language: language
	};

	var aliases = o.alias;
	if (aliases) {
		if (Array.isArray(aliases)) {
			Array.prototype.push.apply(env.classes, aliases);
		} else {
			env.classes.push(aliases);
		}
	}

	_.hooks.run('wrap', env);

	var attributes = '';
	for (var name in env.attributes) {
		attributes += ' ' + name + '="' + (env.attributes[name] || '').replace(/"/g, '&quot;') + '"';
	}

	return '<' + env.tag + ' class="' + env.classes.join(' ') + '"' + attributes + '>' + env.content + '</' + env.tag + '>';
};

/**
 * @param {LinkedList} list
 * @param {string} text
 * @param {any} grammar
 * @param {LinkedListNode} startNode
 * @param {number} startPos
 * @param {RematchOptions} [rematch]
 * @returns {void}
 * @private
 *
 * @typedef {object} RematchOptions
 * @property {string} cause
 * @property {number} reach
 */
function tokenizeXML(list, text, grammar, startNode, startPos, rematch) {
	for (var token in grammar) {
		if (!grammar.hasOwnProperty(token) || !grammar[token]) {
			continue;
		}

		var patterns = grammar[token];
		patterns = (Array.isArray(patterns)) ? patterns : [patterns];

		for (var j = 0; j < patterns.length; ++j) {
			if (rematch && rematch.cause == token + ',' + j) {
				return;
			}

			var pattern = patterns[j],
				inside = pattern.inside,
				lookbehind = !!pattern.lookbehind,
				greedy = !!pattern.greedy,
				alias = pattern.alias;

			if (greedy && !pattern.pattern.global) {
				// Without the global flag, lastIndex won't work
				var flags = pattern.pattern.toString().match(/[imsuy]*$/)[0];
				pattern.pattern = RegExp(pattern.pattern.source, flags + 'g');
			}

			pattern = pattern.pattern || pattern;

			for ( // iterate the token list and keep track of the current position
				var currentNode = startNode.next, pos = startPos;
				currentNode !== list.tail;
				pos += currentNode.value.length, currentNode = currentNode.next
			) {

				if (rematch && pos >= rematch.reach) {
					break;
				}

				var str = currentNode.value;

				if (list.length > text.length) {
					// Something went terribly wrong, the token list can't be longer than the source text.
					return;
				}

				if (str instanceof Token) {
					continue;
				}

				var index = -1;
				var matchedStr = '';

				if (greedy) {
					pattern.lastIndex = pos;
					var match = pattern.exec(text);
					if (!match) {
						break;
					}

					index = match.index + (lookbehind && match[1] ? match[1].length : 0);
					matchedStr = match[0].slice(lookbehind && match[1] ? match[1].length : 0);
					
					var from = pos;
					var to = from + str.length;

					if (index >= to) {
						continue;
					}

					// find the first node overlap the match
					var p = currentNode;
					for (; p !== list.tail && (from < index || typeof p.value === 'string'); p = p.next) {
						from += p.value.length;
					}
					from -= p.value.length;
					pos = from;
					currentNode = p;

					if (currentNode.value instanceof Token) {
						continue;
					}

					// find the last node overlap the match
					var count = 1;
					for (var last = currentNode; last !== list.tail && (from < index + matchedStr.length || typeof last.value === 'string'); last = last.next) {
						count++;
						from += last.value.length;
					}
					count--;
					
					str = text.slice(pos, from);
					index = match.index - pos;
				} else {
					pattern.lastIndex = 0;

					var match = pattern.exec(str);

					if (!match) {
						continue;
					}

					if (lookbehind && match[1]) {
						index = match.index + match[1].length;
					} else {
						index = match.index;
					}

					matchedStr = match[0].slice(lookbehind && match[1] ? match[1].length : 0);
				}

				var before = str.slice(0, index);
				var after = str.slice(index + matchedStr.length);

				var reach = pos + str.length;
				if (rematch && reach > rematch.reach) {
					rematch.reach = reach;
				}

				var prev = currentNode.prev;

				if (before) {
					prev = addAfter(list, prev, before);
					pos += before.length;
				}

				removeRange(list, prev, count || 1);

				currentNode = addAfter(list, prev, new Token(token, inside ? _.highlight(matchedStr, inside, token) : matchedStr, alias, matchedStr));

				if (after) {
					addAfter(list, currentNode, after);
				}

				if (count > 1) {
					var rematchOptions = {
						cause: token + ',' + j,
						reach: reach
					};
					tokenizeXML(list, text, grammar, currentNode.prev, pos, rematchOptions);

					if (rematch && rematchOptions.reach > rematch.reach) {
						rematch.reach = rematchOptions.reach;
					}
				}
			}
		}
	}
}

/**
 * @typedef {object} LinkedListNode
 * @property {any} value
 * @property {LinkedListNode} prev
 * @property {LinkedListNode} next
 */

/**
 * @private
 */
function LinkedList() {
	var head = { value: null, prev: null, next: null };
	var tail = { value: null, prev: head, next: null };
	head.next = tail;

	this.head = head;
	this.tail = tail;
	this.length = 0;
}

/**
 * @param {LinkedList} list
 * @param {LinkedListNode} node
 * @param {any} value
 * @returns {LinkedListNode}
 * @private
 */
function addAfter(list, node, value) {
	var next = node.next;

	var newNode = { value: value, prev: node, next: next };
	node.next = newNode;
	next.prev = newNode;

	list.length++;

	return newNode;
}

/**
 * @param {LinkedList} list
 * @param {LinkedListNode} node
 * @param {number} count
 * @private
 */
function removeRange(list, node, count) {
	var next = node.next;
	for (var i = 0; i < count && next !== list.tail; i++) {
		next = next.next;
	}
	node.next = next;
	next.prev = node;
	list.length -= i;
}

/**
 * @param {LinkedList} list
 * @returns {any[]}
 * @private
 */
function toArray(list) {
	var array = [];
	var node = list.head.next;
	while (node !== list.tail) {
		array.push(node.value);
		node = node.next;
	}
	return array;
}

return _;

})(typeof window !== 'undefined' ? window : (typeof self !== 'undefined' ? self : {}));

if (typeof module !== 'undefined' && module.exports) {
	module.exports = Prism;
}

// LaTeX language definition
Prism.languages.latex = {
	'comment': /%.*/,
	'cdata': {
		pattern: /\\begin\{comment\}[\s\S]*?\\end\{comment\}/,
		inside: {
			'function': /\\(?:begin|end)\{comment\}/,
			'punctuation': /[{}]/
		}
	},
	'keyword': {
		pattern: /\\(?:[a-zA-Z]+|[^a-zA-Z])(?=\{|\[|$)/,
		alias: 'function'
	},
	'url': {
		pattern: /\\url\{.*?\}/,
		inside: {
			'keyword': /\\url/,
			'punctuation': /[{}]/
		}
	},
	'headline': {
		pattern: /(\\(?:part|chapter|section|subsection|subsubsection|paragraph|subparagraph|subsubparagraph|title)(?:\[.*\])?\{)(?:.*)(?=\})/,
		lookbehind: true,
		alias: 'class-name'
	},
	'function': {
		pattern: /\\(?:begin|end)\{[a-zA-Z\*]+\}/,
		inside: {
			'keyword': /\\(?:begin|end)/,
			'punctuation': /[{}]/
		}
	},
	'punctuation': /[[\]{}&]/,
    'string': /\$[^$]*\$/
};
