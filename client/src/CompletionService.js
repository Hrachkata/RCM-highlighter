const vscode = require('vscode');
const acornLoose = require('acorn-loose');
const walk = require('acorn-walk');
const fs = require('fs');
const path = require('path');

var moduleFunctionsCache = {};

async function getJsCompletions(document, position) {
	let block = findJsBlockForLine(document, position);
	if (!block) return;

	try {
		const symbols = extractSymbols(block.code);
		cacheModules(symbols.requires, document.fileName);
		let result = buildCompletionList(symbols);
		return result

	} catch (error) {
		console.error('Completion error:', error);
		return [];
	}
}

function cacheModules(requiresArr, restDir) {
	if (!requiresArr || requiresArr.length == 0) {
		return;
	}

	requiresArr.forEach(element => {
		if (moduleFunctionsCache[element]) {
			return;
		}

		let modulePath = path.resolve('d:\\SSIS+\\install\\files\\JS\\' + element + '.js');

		if (!fs.existsSync(modulePath)) {
			console.warn('Module not found:', modulePath);
		}

		const code = fs.readFileSync(modulePath, 'utf8');
		const functions = extractFunctions(code);

		moduleFunctionsCache[element] = functions;
	});
}

function buildCompletionList(symbols) {
	let all = [];

	symbols.params.forEach(s =>
		all.push({ label: s, kind: 25 })
	);

	symbols.functions.forEach(f =>
		all.push({ label: f, kind: 3 })
	);

	symbols.variables.forEach(v =>
		all.push({ label: v, kind: 6 })
	);

	symbols.properties.forEach(p =>
		all.push({ label: p, kind: 10 })
	);

	// Return functions only on a . character
	symbols.requires.forEach(moduleName => {
		let moduleFunctions = moduleFunctionsCache[moduleName].map(
			funcName => { return { label: funcName, kind: 3 } }
		);
		all = all.concat( moduleFunctions );
	});

	return dedupe(all);
}

function dedupe(items) {
	var seen = {};
	return items.filter(function (item) {
		if (seen[item.label]) return false;
		seen[item.label] = true;
		return true;
	});
}

function findJsBlockForLine(document, position) {
	const text = document.getText();
	const offset = document.offsetAt(position);

	// Find CDATA blocks
	const cdataBlocks = [...text.matchAll(/<!\s*\[CDATA\[([\s\S]*?)\]\]>/g)];
	for (const match of cdataBlocks) {
		const start = match.index + match[0].indexOf('[CDATA[') + 7;
		const end = match.index + match[0].indexOf(']]>');
		if (offset >= start && offset <= end) {
			return {
				code: match[1],
				startLine: document.positionAt(start).line,
				startCol: document.positionAt(start).character,
				startOffset: start,
				endOffset: end
			};
		}
	}

	return null;
}

function extractFunctions(jsCode) {
	const ast = acornLoose.parse(jsCode, {
		ecmaVersion: 5,
		sourceType: 'module'
	});

	const functions = new Set();

	walk.simple(ast, {
		FunctionDeclaration(node) {
			if (node.id) functions.add(node.id.name);
		},

		AssignmentExpression(node) {
			if (node.right.type === 'FunctionExpression') {
				if (node.left.type === 'MemberExpression') {
					const propName = node.left.property.name;
					if (propName) {
						functions.add(propName);
					}
				}
				else if (node.left.type === 'Identifier') {
					functions.add(node.left.name);
				}
			}
		}
	});

	return Array.from(functions);
}

function extractSymbols(jsCode) {
	const ast = acornLoose.parse(jsCode, {
		ecmaVersion: 5,
		sourceType: 'script'
	});

	const symbols = {
		functions: new Set(),
		variables: new Set(),
		properties: new Set(),
		params: new Set(),
		requires: new Set()
	};

	walk.simple(ast, {
		FunctionDeclaration(node) {
			if (node.id) symbols.functions.add(node.id.name);
			node.params.forEach(p => p.name && symbols.params.add(p.name));
		},

		VariableDeclarator(node) {
			if (node.id && node.id.name) {
				symbols.variables.add(node.id.name);
			}
		},

		Property(node) {
			if (node.key && node.key.name) {
				symbols.properties.add(node.key.name);
			}
		},

		AssignmentExpression(node) {
			if (node.right.type === 'FunctionExpression') {
				if (node.left.type === 'MemberExpression') {
					const propName = node.left.property.name;
					if (propName) {
						symbols.functions.add(propName);
					}
					if (node.right.params) {
						node.right.params.forEach(element => {
							symbols.params.add(element.name);
						});
					}
				}
				else if (node.left.type === 'Identifier') {
					symbols.functions.add(node.left.name);
				}
			}
			else if (node.left.type === 'MemberExpression') {
				if (node.left.property && node.left.property.name) {
					symbols.properties.add(node.left.property.name);
				}
			}
		},

		CallExpression(node) {
			if (
				node.callee.name === 'require' &&
				node.arguments.length === 1 &&
				node.arguments[0].type === 'Literal'
			) {
				symbols.requires.add(node.arguments[0].value);
			}
		}
	});

	return {
		functions: Array.from(symbols.functions),
		variables: Array.from(symbols.variables),
		properties: Array.from(symbols.properties),
		params: Array.from(symbols.params),
		requires: Array.from(symbols.requires)
	};
}

module.exports = { getJsCompletions };