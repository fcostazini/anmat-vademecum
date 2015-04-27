package com.thinkupstudios.anmat.vademecum.tasks;

import android.app.Activity;
import android.app.ProgressDialog;
import android.content.Context;
import android.os.AsyncTask;
import android.view.Gravity;

import com.thinkupstudios.anmat.vademecum.UpdateDBActivity;
import com.thinkupstudios.anmat.vademecum.aplicacion.MiAplicacion;
import com.thinkupstudios.anmat.vademecum.exceptions.UpdateNotPosibleException;
import com.thinkupstudios.anmat.vademecum.providers.GenericProvider;
import com.thinkupstudios.anmat.vademecum.providers.PrincipioActivoProvider;
import com.thinkupstudios.anmat.vademecum.providers.SQLiteDBService;
import com.thinkupstudios.anmat.vademecum.providers.VersionProvider;
import com.thinkupstudios.anmat.vademecum.providers.helper.DatabaseHelper;
import com.thinkupstudios.anmat.vademecum.providers.services.contract.IRemoteDBService;
import com.thinkupstudios.anmat.vademecum.providers.tables.MedicamentosTable;
import com.thinkupstudios.anmat.vademecum.providers.tables.PrincipiosActivosTable;

import java.io.IOException;

/**
 * Created by dcamarro on 22/04/2015.
 */
public class UpdateTask extends AsyncTask<Activity, String, String> {

    public static final String OK = "OK";
    private IRemoteDBService dbService;
    private DatabaseHelper dbHelper;
    private UpdateDBActivity updateActivity;
    private ProgressDialog progressDialog;

    public UpdateTask(Context context) {
        super();
        this.dbService = new SQLiteDBService(context);
        this.dbHelper = new DatabaseHelper(context);
        updateActivity = (UpdateDBActivity) context;
        progressDialog = new ProgressDialog(context);

    }


    @Override
    protected String doInBackground(Activity... params) {
        try {

        if (!dbService.isUpToDate()) {

            this.publishProgress("Descargando Actualización");

            dbService.updateDatabase();
            this.publishProgress("Instalando Actualización");

        }
        } catch (UpdateNotPosibleException e) {
            e.printStackTrace();
            return "No se pudo actualizar";

        }
        DatabaseHelper dbHelper = new DatabaseHelper(params[0]);
        ((MiAplicacion)this.updateActivity.getApplication()).updateCache(dbHelper);

        return OK;
    }

    @Override
    protected void onProgressUpdate(String... values) {
        super.onProgressUpdate(values);
        this.progressDialog.setMessage(values[0]);

    }

    @Override
    protected void onPostExecute(String s) {
        super.onPostExecute(s);
        if (progressDialog.isShowing()) {
            progressDialog.dismiss();
        }
        updateActivity.continuar();
    }

    @Override
    protected void onPreExecute() {
        super.onPreExecute();
        progressDialog.setMessage("Buscando Actualizaciones...");
        progressDialog.getWindow().setGravity(Gravity.BOTTOM);
        progressDialog.show();

    }

}
