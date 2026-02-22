const acornLoose = require('acorn-loose');
const walk = require('acorn-walk');
const fs = require('fs');
const path = require('path');

const moduleGlobalsCache = new Map();

// Extracts possible global identifiers produced by a module.
function cacheModules(requiresArr, restDir) {
	if (!requiresArr || requiresArr.length == 0) {
		return;
	}

	requiresArr.forEach(element => {
		if (!moduleGlobalsCache.has(element)) {
			try {
				// Go back 2 directories
				let parentDir = path.dirname(path.dirname(restDir));
				let modulePath = path.resolve(parentDir + '\\JS\\' + element + '.js');

				if (!fs.existsSync(modulePath)) {
					console.warn('Module not found:', modulePath);
					return [];
				}

				const code = fs.readFileSync(modulePath, 'utf8');
				const moduleGlobals = walkExtract(code);

				moduleGlobalsCache.set(element, moduleGlobals);
			} catch (error) {
			}
		}
	});

	const allGlobalsSet = new Set([...moduleGlobalsCache.values()].flatMap(set => [...set]));

	return allGlobalsSet;
}


function collectRequireModules(jsCode) {
	const ast = acornLoose.parse(jsCode, {
		ecmaVersion: 5,
		sourceType: 'script'
	});

	const requires = new Set();

	walk.simple(ast, {
		CallExpression(node) {
			if (
				node.callee.name === 'require' &&
				node.arguments.length === 1 &&
				node.arguments[0].type === 'Literal'
			) {
				requires.add(node.arguments[0].value);
			}
		}
	});

	return requires;
}

function extractGlobalIdentifiers(jsCode, fileName) {
	let moduleNames = collectRequireModules(jsCode);
	return cacheModules(moduleNames, fileName);
}


function walkExtract(jsCode) {
	const globals = new Set();

	// Identifiers that are assumed to point to the global object
	const globalAliases = new Set([
		'global',
		'window',
		'self'
	]);

	const ast = acornLoose.parse(jsCode, {
		ecmaVersion: '5',
		sourceType: 'script',
		allowReturnOutsideFunction: true
	});

	walk.fullAncestor(ast, (node, ancestors) => {
		// Only consider top-level statements

		// ------------------------------------------------------------
        // Naked functions detection, e.g sha256
        // ------------------------------------------------------------
		if (
			node.type === 'FunctionDeclaration'
		) {
			globals.add(node.id.name);
			return;
		}

		// ------------------------------------------------------------
		// 1. Detect global aliasing: var root = this;
		// ------------------------------------------------------------
		if (
			node.type === 'VariableDeclarator' &&
			node.id.type === 'Identifier' &&
			node.init &&
			(
				node.init.type === 'ThisExpression' ||
				(node.init.type === 'LogicalExpression' &&
					containsThisExpression(node.init))
			)
		) {
			globalAliases.add(node.id.name);
			return;
		}

		// ------------------------------------------------------------
		// 2. Detect: var Google = {};
		// ------------------------------------------------------------
		if (
			node.type === 'VariableDeclarator' &&
			node.id.type === 'Identifier' &&
			node.init &&
			(
				node.init.type === 'ObjectExpression' ||
				node.init.type === 'FunctionExpression' ||
				node.init.type === 'ArrowFunctionExpression'
			)
		) {
			globals.add(node.id.name);
			return;
		}

		// ------------------------------------------------------------
		// 3. Detect: root.X = ..., global.X = ..., this.X = ...
		// ------------------------------------------------------------
		if (
			node.type === 'AssignmentExpression' &&
			node.left.type === 'MemberExpression' &&
			!node.left.computed &&
			node.left.object.type === 'Identifier' &&
			globalAliases.has(node.left.object.name) &&
			node.left.property.type === 'Identifier'
		) {
			globals.add(node.left.property.name);
			return;
		}
	});

	return globals;
}

function containsThisExpression(node) {
	let found = false;

	walk.simple(node, {
		ThisExpression() {
			found = true;
		}
	});

	return found;
}

module.exports = { extractGlobalIdentifiers };