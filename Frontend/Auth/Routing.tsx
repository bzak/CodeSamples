import "es6-promise"
import "Commons/polyfills"

import React = require("react");
import ReactDOM = require("react-dom");
import Redux = require("redux");
import * as i18n from "i18n/translate"
import { thunkMiddleware } from "Commons/Basics"

import { createStore, combineReducers } from 'redux'
import { Provider } from 'react-redux'
import { Router, Route, IndexRoute, browserHistory } from 'react-router'
import { syncHistoryWithStore, routerReducer, routerMiddleware } from 'react-router-redux'
import 'redux-logger'; let createLogger = require("redux-logger");

import { reducer as authReducer } from "Auth/Actions"
import { Auth } from "Auth/Auth"
import { default as EmailForm } from "Auth/EmailForm"
import { default as LoginForm } from "Auth/LoginForm"
import { default as RegisterForm } from "Auth/RegisterForm"
import { default as ShowDemo } from "Auth/ShowDemo"
import { default as DemoSignUp } from "Auth/Demo"
import { default as DemoSubmitted } from "Auth/DemoSubmitted"
import { default as ConfirmEmail } from "Auth/ConfirmEmail"
import { default as ConfirmEmailSent } from "Auth/ConfirmEmailSent"
import { default as RecoverPasswordForm, RecoverPasswordFormSubmitted } from "Auth/RecoverPasswordForm"
import { default as ResetPasswordForm } from "Auth/ResetPasswordForm"
import { default as ValidateInvite } from "Auth/ValidateInvite"
import { default as ValidateEmail } from "Auth/ValidateEmail"
import { default as Expired } from "Auth/Expired"
import { default as External } from "Auth/External"

export function initialize(config: any) {
    if ((window as any).appInsights) {
        browserHistory.listen(location => {
            (window as any).appInsights.trackPageView();
        });
    }
    let middlewares = [thunkMiddleware, routerMiddleware(browserHistory)];
    if ((window as any).JsConsoleLogging) {
        const logger = createLogger({ diff: true, collapsed: true });
        middlewares.push(logger);
    }
    const createStore = Redux.applyMiddleware(...middlewares)(Redux.createStore);

    // Apply the middleware to the store
    i18n.initialize(config.language, () => {

        var store = createStore(
            combineReducers({
                auth: authReducer,                
                routing: routerReducer
            })
        );
        
        const history = syncHistoryWithStore(browserHistory, store)
        
        function render() {
            ReactDOM.render(
                <Provider store={store}>                                        
                    <Router history={history}>
                        <Route path="auth" component={Auth}>
                            <IndexRoute component={EmailForm}/>
                            <Route path="password" component={LoginForm}/>
                            <Route path="register" component={RegisterForm}/>
                            <Route path="show-demo" component={ShowDemo}/>
                            <Route path="demo" component={DemoSignUp}/>
                            <Route path="demo-submitted" component={DemoSubmitted}/>
                            <Route path="confirm-email" component={ConfirmEmail}/>
                            <Route path="confirm-email-sent" component={ConfirmEmailSent}/>
                            <Route path="recover-password" component={RecoverPasswordForm}/>                            
                            <Route path="recover-password-submitted" component={RecoverPasswordFormSubmitted}/>
                            <Route path="reset-password" component={ResetPasswordForm}/>
                            <Route path="invite/:token" component={ValidateInvite}/>
                            <Route path="email" component={ValidateEmail}/>
                            <Route path="expired" component={Expired}/>
                            <Route path="external" component={External}/>
                        </Route>
                    </Router>
                </Provider>
                ,
                document.getElementById("app")
            );
        }

        render();
        
        store.subscribe(render);
        
    });

}

