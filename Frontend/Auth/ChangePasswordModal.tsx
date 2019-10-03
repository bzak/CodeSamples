import * as React from "react"
import { _extends } from "../Commons/basics"
import { AccountApi } from "../api/api"
import { success } from "../Commons/actions"
import { t } from "../i18n/translate"


export default class ChangePasswordModal extends React.Component<{ userName: string },
    { currentPassword: string, newPassword: string, currentPasswordError: string, newPasswordError: string, error: string }> {
    constructor(props) {
        super(props);
        this.state = {
            currentPassword: null,
            currentPasswordError: null,
            newPassword: null,
            newPasswordError: null,
            error: null
        }
    }
    submit(e) {
        e.preventDefault();
        let currentPasswordError = null;
        let newPasswordError = null;
        let error = null;
        if (!this.state.currentPassword || this.state.currentPassword.length == 0)            
            currentPasswordError = t("Auth:This field is required")
            
        if (!this.state.newPassword || this.state.newPassword.length == 0)            
            newPasswordError = t("Auth:This field is required")

        this.setState(_extends({}, this.state, {
            currentPasswordError, newPasswordError, error
        }));

        if (!(currentPasswordError || newPasswordError)) {
            let api = new AccountApi();
            api.accountChangePassword({
                command: {
                    currentPassword: this.state.currentPassword,
                    newPassword: this.state.newPassword
                }
            })
            .then(result => {
                success(t("Auth:Your password has been changed"));
                ($('#changePasswordModal') as any).modal('hide')
                return null;
            })
            .catch(error => {
                if (error.status == 400) {
                    return error.json();
                } else {
                    throw error;
                }
            })
            .then(error => {                                                        
                this.setState(_extends({}, this.state, {
                    error: error ? error.Message : null
                }));
            });
        }
    }
    reset() {
        this.setState(_extends({}, this.state, {
            currentPassword: null,
            currentPasswordError: null,
            newPassword: null,
            newPasswordError: null
        }));
    }
    setCurrentPassword(currentPassword: string) {
        this.setState(_extends({}, this.state, {
            currentPassword, currentPasswordError: null
        }));
    }
    setNewPassword(newPassword: string) {
        this.setState(_extends({}, this.state, {
            newPassword, newPasswordError: null
        }));
    }
    render() {
        return (
            <div className="modal fade" id="changePasswordModal" tabIndex={-1} role="dialog" aria-labelledby="changePasswordModal">
                <div className="modal-dialog" role="document">
                    <div className="modal-content">
                        <form onSubmit={(e) => this.submit(e)}>
                        <div className="modal-header">
                            <button type="button" className="close" data-dismiss="modal" onClick={e => this.reset()} aria-label="Close"><span aria-hidden="true">&times;</span></button>
                            <h4 className="modal-title" id="myModalLabel">{t("Auth:Change password")}</h4>
                        </div>
                        <div className="modal-body">
                            <p>
                                {t("Auth:Choose a unique password to protect your account")}.
                            </p>
                            <input type="text" name="username" value={this.props.userName} className="hidden" readOnly />
                            {this.state.error &&
                                <div className="alert alert-danger" role="alert">{this.state.error}</div>
                            }
                            <div className={"form-group" + ((this.state.currentPasswordError) ? " has-error" : "")}>
                                <label className="control-label" htmlFor="perspectiveName">{t("Auth:Current password")}</label>
                                <input type="password" className="form-control" 
                                    onChange={e => this.setCurrentPassword((e as any).target.value)}
                                    value={this.state.currentPassword} />
                                {(this.state.currentPasswordError) &&
                                    <span className="help-block">{this.state.currentPasswordError}</span>
                                }
                            </div>
                            <div className={"form-group" + ((this.state.newPasswordError) ? " has-error" : "")}>
                                <label className="control-label" htmlFor="perspectiveName">{t("Auth:New password")}</label>
                                <input type="password" className="form-control" name="password"
                                    onChange={e => this.setNewPassword((e as any).target.value)}
                                    value={this.state.newPassword} />
                                {(this.state.newPasswordError) &&
                                    <span className="help-block">{this.state.newPasswordError}</span>
                                }
                            </div>
                        </div>
                        <div className="modal-footer">
                            <button type="button" className="btn btn-default" onClick={e => this.reset()} data-dismiss="modal">{t("Auth:Cancel")}</button>
                            <button type="submit" className="btn btn-primary">{t("Auth:Save")}</button>
                        </div>
                        </form>
                    </div>
                </div>
            </div>
        )
    }
}